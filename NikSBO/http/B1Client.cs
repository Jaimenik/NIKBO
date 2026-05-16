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
    public class B1Client
    {
        private HttpClient _client;
        private B1Options _options;
        private Auth _auth;
        public DateTimeOffset? ExpiresAt => _auth?.ExpiresAt;
        public bool IsExpired(TimeSpan? safetyMargin = null) =>
                    _auth?.IsExpired(safetyMargin) ?? true;

        public B1Client(B1Options options)
        {
            this._options = options;
        }

        public async Task Login()
        {
            if (_auth is not null && !_auth.IsExpired())
                return;

            _auth = new Auth(_options.ServerUrl);
            await _auth.Login(_options.Username, _options.Password, _options.CompanyDb);
            this._client = _auth.HttpClient;
        }

        public async Task Logout()
        {
            await _auth.Logout();
        }

        public Task<HttpResponseMessage> ExecuteAsync(Func<HttpClient, Task<HttpResponseMessage>> request)
        {
            return SendWithAuthAsync(() => request(_client));
        }

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

        public async Task<T> GetByEndpointAsync<T>(string endpoint)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;
            var response = await SendWithAuthAsync(() => _client.GetAsync(endpoint));
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);

            return (await response.Content.ReadFromJsonAsync<T>())!;
        }

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

        // GET por clave string
        public async Task<T> GetAsync<T>(string key)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}('{key}')";
            return await GetByEndpointAsync<T>(endpoint);
        }

        // GET por clave numérica
        public async Task<T> GetAsync<T>(int key)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}({key})";
            return await GetByEndpointAsync<T>(endpoint);
        }

        // POST sin endpoint
        public async Task<T> PostAsync<T>(object body)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = "b1s/v1/" + atributo.Endpoint;
            return await PostByEndpointAsync<T>(endpoint, body);
        }

        // PATCH por clave string
        public async Task PatchAsync<T>(string key, object body)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}('{key}')";
            await PatchByEndpointAsync(endpoint, body);
        }

        // PATCH por clave numérica
        public async Task PatchAsync<T>(int key, object body)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}({key})";
            await PatchByEndpointAsync(endpoint, body);
        }

        // DELETE por clave string
        public async Task DeleteAsync<T>(string key)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}('{key}')";
            await DeleteByEndpointAsync(endpoint);
        }

        // DELETE por clave numérica
        public async Task DeleteAsync<T>(int key)
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = $"b1s/v1/{atributo.Endpoint}({key})";
            await DeleteByEndpointAsync(endpoint);
        }
        
        public B1Query<T> Query<T>(string endpoint)
        {
            if (!endpoint.StartsWith("b1s/v1/"))
                endpoint = "b1s/v1/" + endpoint;
            return new B1Query<T>(this, endpoint);
        }
        #endregion

        #region Query y Batch

        public B1Query<T> Query<T>()
        {
            var atributo = typeof(T).GetCustomAttribute<B1EntityAttribute>();
            var endpoint = "b1s/v1/" + atributo.Endpoint;
            return new B1Query<T>(this, endpoint);
        }


        public B1Batch CreateBatch()
        {
            return new B1Batch(this);
        }

        #endregion
    }
}