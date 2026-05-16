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

namespace NikSBO.http
{
    /// <summary>
    /// Cliente principal del SDK contra el Service Layer de SAP Business One.
    /// Gestiona la sesión (login y renovación automática), expone CRUD tipado y por endpoint,
    /// y abre <see cref="B1Query{T}"/> y <see cref="B1Batch"/>.
    /// </summary>
    public class B1Client
    {
        private HttpClient _client;
        private B1Options _options;
        private Auth _auth;

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
        public async Task Login()
        {
            if (_auth is not null && !_auth.IsExpired())
                return;

            _auth = new Auth(_options.ServerUrl);
            await _auth.Login(_options.Username, _options.Password, _options.CompanyDb);
            this._client = _auth.HttpClient;
        }

        /// <summary>Cierra la sesión actual contra el Service Layer.</summary>
        public async Task Logout()
        {
            await _auth.Logout();
        }

        /// <summary>
        /// Ejecuta una petición HTTP arbitraria a través del flujo de autenticación: renueva
        /// la sesión si está caducada y reintenta una vez si el SL responde 401.
        /// </summary>
        /// <param name="request">Lambda que recibe el <see cref="HttpClient"/> con la sesión inyectada.</param>
        public Task<HttpResponseMessage> ExecuteAsync(Func<HttpClient, Task<HttpResponseMessage>> request)
        {
            return SendWithAuthAsync(() => request(_client));
        }

        /// <summary>
        /// Ejecuta SQL crudo contra SAP creando, ejecutando y borrando un <c>SQLQueries</c> de forma transparente.
        /// <para>
        /// Cuidado: el SQL se concatena tal cual, sin parametrizar. No lo uses con valores
        /// controlados por el usuario hasta que el SDK añada soporte de parámetros.
        /// </para>
        /// </summary>
        /// <param name="sql">Sentencia SQL a ejecutar.</param>
        /// <returns>El JSON crudo (sin tipar) que devuelve <c>/SQLQueries('NAME')/List</c>.</returns>
        public async Task<object> SqlAsync(string sql)
        {
            var queryName = "SDK_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            // Crear
            await PostByEndpointAsync<object>("SQLQueries", new
            {
                SqlCode = queryName,
                SqlName = queryName,
                SqlText = sql
            });

            // Ejecutar
            var result = await GetByEndpointAsync<object>($"SQLQueries('{queryName}')/List");

            // Borrar
            await DeleteByEndpointAsync($"SQLQueries('{queryName}')");

            return result;
        }

        /// <summary>
        /// Método que utilizo para poder verificar que la petición que está enviando el usuario no tiene un sessionId expirado
        /// </summary>
        /// <param name="sendRequest"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task<HttpResponseMessage> SendWithAuthAsync(Func<Task<HttpResponseMessage>> sendRequest)
        {
            if (_auth is null)
                throw new InvalidOperationException("Llama a Login() primero.");

            if (_auth.IsExpired())
                await _auth.Login(_options.Username, _options.Password, _options.CompanyDb);

            var response = await sendRequest();

            // Cinturón y tirantes: si el SL nos devuelve 401 igualmente
            // (reloj desincronizado, sesión invalidada por admin, etc.), reintenta.
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await _auth.Login(_options.Username, _options.Password, _options.CompanyDb);
                response = await sendRequest();
            }

            return response;
        }

        #region Métodos base (con endpoint manual)

        /// <summary>GET contra el endpoint indicado y deserializa la respuesta a <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Tipo al que deserializar la respuesta.</typeparam>
        /// <param name="endpoint">Endpoint relativo (con o sin el prefijo <c>b1s/v1/</c>).</param>
        public async Task<T> GetByEndpointAsync<T>(string endpoint)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;
            var response = await SendWithAuthAsync(() => _client.GetAsync(endpoint));
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);

            return (await response.Content.ReadFromJsonAsync<T>())!;
        }

        /// <summary>POST con cuerpo JSON contra el endpoint indicado y deserializa la respuesta a <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Tipo al que deserializar la respuesta (típicamente la entidad creada).</typeparam>
        /// <param name="endpoint">Endpoint relativo (con o sin el prefijo <c>b1s/v1/</c>).</param>
        /// <param name="body">Cuerpo a serializar a JSON.</param>
        public async Task<T> PostByEndpointAsync<T>(string endpoint, object body)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;

            var json = System.Text.Json.JsonSerializer.Serialize(body);
            var response = await SendWithAuthAsync(() =>
                    _client.PostAsync(endpoint, new StringContent(json, Encoding.UTF8, "application/json")));
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);

            return (await response.Content.ReadFromJsonAsync<T>())!;
        }

        /// <summary>PATCH (actualización parcial) con cuerpo JSON contra el endpoint indicado.</summary>
        /// <param name="endpoint">Endpoint relativo con clave, ej. <c>"BusinessPartners('C001')"</c>.</param>
        /// <param name="body">Cuerpo a serializar a JSON con los campos a actualizar.</param>
        public async Task PatchByEndpointAsync(string endpoint, object body)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;

            var json = System.Text.Json.JsonSerializer.Serialize(body);
            var response = await SendWithAuthAsync(() =>
                    _client.PatchAsync(endpoint, new StringContent(json, Encoding.UTF8, "application/json")));
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);
        }

        /// <summary>DELETE contra el endpoint indicado.</summary>
        /// <param name="endpoint">Endpoint relativo con clave, ej. <c>"BusinessPartners('C001')"</c>.</param>
        public async Task DeleteByEndpointAsync(string endpoint)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;

            var response = await SendWithAuthAsync(() => _client.DeleteAsync(endpoint));
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);
        }

        #endregion

        #region Sobrecargas con B1Entity (sin endpoint manual)

        /// <summary>GET por clave string. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria string (ej. <c>"C30000"</c>).</param>
        public async Task<T> GetAsync<T>(string key)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}('{key}')";
            return await GetByEndpointAsync<T>(endpoint);
        }

        /// <summary>GET por clave numérica. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria numérica (ej. <c>DocEntry</c>).</param>
        public async Task<T> GetAsync<T>(int key)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}({key})";
            return await GetByEndpointAsync<T>(endpoint);
        }

        /// <summary>POST para crear una nueva entidad. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="body">Entidad o anónimo a serializar a JSON.</param>
        public async Task<T> PostAsync<T>(object body)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = "b1s/v1/" + atributo.Endpoint;
            return await PostByEndpointAsync<T>(endpoint, body);
        }

        /// <summary>PATCH por clave string. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria string.</param>
        /// <param name="body">Cuerpo a serializar a JSON con los campos a actualizar.</param>
        public async Task PatchAsync<T>(string key, object body)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}('{key}')";
            await PatchByEndpointAsync(endpoint, body);
        }

        /// <summary>PATCH por clave numérica. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria numérica.</param>
        /// <param name="body">Cuerpo a serializar a JSON con los campos a actualizar.</param>
        public async Task PatchAsync<T>(int key, object body)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}({key})";
            await PatchByEndpointAsync(endpoint, body);
        }

        /// <summary>DELETE por clave string. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria string.</param>
        public async Task DeleteAsync<T>(string key)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}('{key}')";
            await DeleteByEndpointAsync(endpoint);
        }

        /// <summary>DELETE por clave numérica. Resuelve el endpoint desde <see cref="B1EntityAttribute"/>.</summary>
        /// <typeparam name="T">Tipo del modelo decorado con <see cref="B1EntityAttribute"/>.</typeparam>
        /// <param name="key">Clave primaria numérica.</param>
        public async Task DeleteAsync<T>(int key)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}({key})";
            await DeleteByEndpointAsync(endpoint);
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
    }
}
