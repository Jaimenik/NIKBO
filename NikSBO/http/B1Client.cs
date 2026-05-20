using NikSBO.Exceptions;
using NikSBO.models;
using NikSBO.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
#if NETSTANDARD2_0
using NikSBO.Compat;   // polyfill: HttpClient.PatchAsync no existe en netstandard2.0
#endif

namespace NikSBO.http
{
    /// <summary>
    /// Cliente principal del SDK contra el Service Layer de SAP Business One.
    /// Gestiona la sesión (login y renovación automática), expone CRUD tipado y por endpoint,
    /// y abre <see cref="B1Query{T}"/> y <see cref="B1Batch"/>.
    /// Implementa <see cref="IAsyncDisposable"/> para que <c>await using</c> haga Logout automático.
    /// </summary>
    public class B1Client : IAsyncDisposable
    {
        private HttpClient _client;
        private B1Options _options;
        private Auth _auth;
        private bool _disposed;

        /// <summary>Momento UTC en el que se prevé que caduque la sesión actual, o <c>null</c> si no se ha hecho login.</summary>
        public DateTimeOffset? ExpiresAt => _auth?.ExpiresAt;

        /// <summary>Indica si la sesión está caducada (o si todavía no se ha hecho login).</summary>
        /// <param name="safetyMargin">Margen de seguridad a restar al tiempo de caducidad.</param>
        public bool IsExpired(TimeSpan? safetyMargin = null) =>
                    _auth?.IsExpired(safetyMargin) ?? true;

        /// <summary>
        /// Crea el cliente con los parámetros de conexión. No se conecta hasta llamar a <see cref="Login"/>.
        /// </summary>
        /// <param name="options">Configuración de conexión al Service Layer.</param>
        public B1Client(B1Options options)
        {
            this._options = options;
        }

        /// <summary>
        /// Hace login contra el Service Layer si no hay sesión activa o si la actual ya caducó.
        /// </summary>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task Login(CancellationToken cancellationToken = default)
        {
            if (_auth is not null && !_auth.IsExpired())
                return;

            _auth = new Auth(_options.ServerUrl, _options.AcceptAnyServerCertificate);
            await _auth.Login(_options.Username, _options.Password, _options.CompanyDb, cancellationToken);
            this._client = _auth.HttpClient;
        }

        /// <summary>Cierra la sesión actual contra el Service Layer.</summary>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task Logout(CancellationToken cancellationToken = default)
        {
            await _auth.Logout(cancellationToken);
        }

        /// <summary>
        /// Ejecuta una petición HTTP arbitraria a través del flujo de autenticación: renueva
        /// la sesión si está caducada y reintenta una vez si el SL responde 401.
        /// </summary>
        /// <param name="request">Lambda que recibe el <see cref="HttpClient"/> con la sesión inyectada y el <see cref="CancellationToken"/> para pasarlo a la llamada HTTP.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso (también disponible dentro del lambda).</param>
        public Task<HttpResponseMessage> ExecuteAsync(Func<HttpClient, CancellationToken, Task<HttpResponseMessage>> request, CancellationToken cancellationToken = default)
        {
            return SendWithAuthAsync(() => request(_client, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Ejecuta SQL crudo contra SAP creando, ejecutando y borrando un <c>SQLQueries</c> de forma transparente.
        /// <para>
        /// Cuidado: el SQL se concatena tal cual, sin parametrizar. No lo uses con valores
        /// controlados por el usuario hasta que el SDK añada soporte de parámetros.
        /// </para>
        /// </summary>
        /// <param name="sql">Sentencia SQL a ejecutar.</param>
        /// <param name="cancellationToken">Token para cancelar la operación. Si se cancela tras crear el <c>SQLQueries</c> puede quedar huérfano en SAP.</param>
        /// <returns>El JSON crudo (sin tipar) que devuelve <c>/SQLQueries('NAME')/List</c>.</returns>
        public async Task<object> SqlAsync(string sql, CancellationToken cancellationToken = default)
        {
            var queryName = "SDK_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            // Crear
            await PostByEndpointAsync<object>("SQLQueries", new
            {
                SqlCode = queryName,
                SqlName = queryName,
                SqlText = sql
            }, cancellationToken);

            // Ejecutar
            var result = await GetByEndpointAsync<object>($"SQLQueries('{queryName}')/List", cancellationToken);

            // Borrar
            await DeleteByEndpointAsync($"SQLQueries('{queryName}')", cancellationToken);

            return result;
        }

        /// <summary>
        /// Método que utilizo para poder verificar que la petición que está enviando el usuario no tiene un sessionId expirado
        /// </summary>
        /// <param name="sendRequest"></param>
        /// <param name="cancellationToken">Token para cancelar tanto el re-login como la petición original.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task<HttpResponseMessage> SendWithAuthAsync(Func<Task<HttpResponseMessage>> sendRequest, CancellationToken cancellationToken = default)
        {
            if (_auth is null)
                throw new InvalidOperationException("Llama a Login() primero.");

            if (_auth.IsExpired())
                await _auth.Login(_options.Username, _options.Password, _options.CompanyDb, cancellationToken);

            var response = await sendRequest();

            // Cinturón y tirantes: si el SL nos devuelve 401 igualmente
            // (reloj desincronizado, sesión invalidada por admin, etc.), reintenta.
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await _auth.Login(_options.Username, _options.Password, _options.CompanyDb, cancellationToken);
                response = await sendRequest();
            }

            return response;
        }

        #region Métodos base (con endpoint manual)

        /// <summary>GET contra el endpoint indicado y deserializa la respuesta a <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Tipo al que deserializar la respuesta.</typeparam>
        /// <param name="endpoint">Endpoint relativo (con o sin el prefijo <c>b1s/v1/</c>).</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task<T> GetByEndpointAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;
            var response = await SendWithAuthAsync(() => _client.GetAsync(endpoint, cancellationToken), cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);

            return (await response.Content.ReadFromJsonAsync<T>(options: null, cancellationToken: cancellationToken))!;
        }

        /// <summary>POST con cuerpo JSON contra el endpoint indicado y deserializa la respuesta a <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Tipo al que deserializar la respuesta (típicamente la entidad creada).</typeparam>
        /// <param name="endpoint">Endpoint relativo (con o sin el prefijo <c>b1s/v1/</c>).</param>
        /// <param name="body">Cuerpo a serializar a JSON.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task<T> PostByEndpointAsync<T>(string endpoint, object body, CancellationToken cancellationToken = default)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;

            var json = System.Text.Json.JsonSerializer.Serialize(body);
            var response = await SendWithAuthAsync(() =>
                    _client.PostAsync(endpoint, new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken), cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);

            return (await response.Content.ReadFromJsonAsync<T>(options: null, cancellationToken: cancellationToken))!;
        }

        /// <summary>PATCH (actualización parcial) con cuerpo JSON contra el endpoint indicado.</summary>
        /// <param name="endpoint">Endpoint relativo con clave, ej. <c>"BusinessPartners('C001')"</c>.</param>
        /// <param name="body">Cuerpo a serializar a JSON con los campos a actualizar.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task PatchByEndpointAsync(string endpoint, object body, CancellationToken cancellationToken = default)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;

            var json = System.Text.Json.JsonSerializer.Serialize(body);
            var response = await SendWithAuthAsync(() =>
                    _client.PatchAsync(endpoint, new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken), cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);
        }

        /// <summary>DELETE contra el endpoint indicado.</summary>
        /// <param name="endpoint">Endpoint relativo con clave, ej. <c>"BusinessPartners('C001')"</c>.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task DeleteByEndpointAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;

            var response = await SendWithAuthAsync(() => _client.DeleteAsync(endpoint, cancellationToken), cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);
        }

        #endregion

        #region Sobrecargas con B1Entity (sin endpoint manual)

        /// <summary>GET por clave string. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria string (ej. <c>"C30000"</c>).</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}('{key}')";
            return await GetByEndpointAsync<T>(endpoint, cancellationToken);
        }

        /// <summary>GET por clave numérica. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria numérica (ej. <c>DocEntry</c>).</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task<T> GetAsync<T>(int key, CancellationToken cancellationToken = default)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}({key})";
            return await GetByEndpointAsync<T>(endpoint, cancellationToken);
        }

        /// <summary>POST para crear una nueva entidad. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="body">Entidad o anónimo a serializar a JSON.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task<T> PostAsync<T>(object body, CancellationToken cancellationToken = default)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = "b1s/v1/" + atributo.Endpoint;
            return await PostByEndpointAsync<T>(endpoint, body, cancellationToken);
        }

        /// <summary>PATCH por clave string. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria string.</param>
        /// <param name="body">Cuerpo a serializar a JSON con los campos a actualizar.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task PatchAsync<T>(string key, object body, CancellationToken cancellationToken = default)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}('{key}')";
            await PatchByEndpointAsync(endpoint, body, cancellationToken);
        }

        /// <summary>PATCH por clave numérica. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria numérica.</param>
        /// <param name="body">Cuerpo a serializar a JSON con los campos a actualizar.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task PatchAsync<T>(int key, object body, CancellationToken cancellationToken = default)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}({key})";
            await PatchByEndpointAsync(endpoint, body, cancellationToken);
        }

        /// <summary>DELETE por clave string. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria string.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task DeleteAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}('{key}')";
            await DeleteByEndpointAsync(endpoint, cancellationToken);
        }

        /// <summary>DELETE por clave numérica. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria numérica.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task DeleteAsync<T>(int key, CancellationToken cancellationToken = default)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}({key})";
            await DeleteByEndpointAsync(endpoint, cancellationToken);
        }

        /// <summary>
        /// Abre un query builder OData contra un endpoint manual. Útil para UDOs o recursos sin modelo.
        /// </summary>
        /// <typeparam name="T">Tipo al que deserializar cada elemento del array <c>value</c>.</typeparam>
        /// <param name="endpoint">Endpoint relativo, ej. <c>"MY_UDO"</c>.</param>
        public B1Query<T> Query<T>(string endpoint)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;
            return new B1Query<T>(this, endpoint);
        }
        #endregion

        #region Acciones de documento

        /// <summary>
        /// Invoca una acción del Service Layer sobre un documento (<c>POST /Endpoint(key)/Action</c>).
        /// Las acciones de SAP B1 cambian el estado del documento (<c>Close</c>, <c>Cancel</c>,
        /// <c>MarkAsClosed</c>, etc.) y normalmente no llevan cuerpo.
        /// </summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria numérica del documento (típicamente <c>DocEntry</c>).</param>
        /// <param name="action">Nombre de la acción tal como la expone el Service Layer, sin slashes.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task InvokeActionAsync<T>(int key, string action, CancellationToken cancellationToken = default)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            await InvokeActionByEndpointAsync($"{atributo.Endpoint}({key})", action, cancellationToken);
        }

        /// <summary>
        /// Invoca una acción del Service Layer sobre un documento con clave string
        /// (<c>POST /Endpoint('key')/Action</c>).
        /// </summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria string.</param>
        /// <param name="action">Nombre de la acción.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task InvokeActionAsync<T>(string key, string action, CancellationToken cancellationToken = default)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            await InvokeActionByEndpointAsync($"{atributo.Endpoint}('{key}')", action, cancellationToken);
        }

        /// <summary>
        /// Invoca una acción contra un endpoint manual. Útil para UDOs o recursos sin modelo.
        /// </summary>
        /// <param name="endpoint">Endpoint relativo con la clave del recurso, ej. <c>"MY_UDO('R001')"</c>.</param>
        /// <param name="action">Nombre de la acción.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public async Task InvokeActionByEndpointAsync(string endpoint, string action, CancellationToken cancellationToken = default)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;
            var url = $"{endpoint}/{action}";

            var response = await SendWithAuthAsync(
                () => _client.PostAsync(url, content: null, cancellationToken), cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);
        }

        /// <summary>
        /// Atajo para la acción <c>Close</c>. Equivale a <see cref="InvokeActionAsync{T}(int, string, CancellationToken)"/>
        /// con <c>"Close"</c> como acción.
        /// </summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria numérica del documento.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public Task CloseAsync<T>(int key, CancellationToken cancellationToken = default)
            => InvokeActionAsync<T>(key, "Close", cancellationToken);

        /// <summary>
        /// Atajo para la acción <c>Close</c> con clave string.
        /// </summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria string del documento.</param>
        /// <param name="cancellationToken">Token para cancelar la petición en curso.</param>
        public Task CloseAsync<T>(string key, CancellationToken cancellationToken = default)
            => InvokeActionAsync<T>(key, "Close", cancellationToken);

        #endregion

        #region Query y Batch

        /// <summary>
        /// Abre un query builder OData usando el endpoint declarado en <see cref="B1EntityAttribute"/>
        /// del tipo <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        public B1Query<T> Query<T>()
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = "b1s/v1/" + atributo.Endpoint;
            return new B1Query<T>(this, endpoint);
        }


        /// <summary>Crea un nuevo <see cref="B1Batch"/> para acumular operaciones y enviarlas como una transacción atómica.</summary>
        public B1Batch CreateBatch()
        {
            return new B1Batch(this);
        }

        #endregion

        /// <summary>
        /// Cierra la sesión activa (si la hay) en best-effort y libera el <see cref="HttpClient"/>.
        /// Idempotente. Las excepciones durante el Logout se silencian — Dispose nunca debe lanzar.
        /// Si necesitas saber que el Logout funcionó, llama a <see cref="Logout"/> explícitamente
        /// antes del dispose.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            if (_auth is not null && !_auth.IsExpired())
            {
                try { await _auth.Logout(); }
                catch { /* swallow: Dispose nunca debe lanzar */ }
            }

            _auth?.Dispose();
        }
    }
}
