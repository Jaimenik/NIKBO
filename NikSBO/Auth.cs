using NikSBO.Exceptions;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace NikSBO
{
    public record LoginRequest(string UserName, string Password, string CompanyDB);
    public record LoginResponse(string SessionId, int SessionTimeout, string Version);
   

    public class Auth
    {
        private readonly CookieContainer _cookies;
        private readonly HttpClientHandler _handler;
        private readonly HttpClient _client;
        private string? _sessionId;
        private int _sessionTimeout;
        private string? _version;
        public int SessionTimeoutMinutes => _sessionTimeout;
        public string? Version => _version;
        private DateTimeOffset _loginAt;
        public DateTimeOffset? ExpiresAt =>
                 _sessionId is null ? null : _loginAt.AddMinutes(_sessionTimeout);


        public Auth(string uri)
        {
            _cookies = new CookieContainer();
            _handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = _cookies,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            _client = new HttpClient(_handler)
            {
                BaseAddress = new Uri(uri)
            };
            _client.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task Login(string username, string password, string companyDB)
        {
            var credenciales = new LoginRequest(username, password, companyDB);
            var json = System.Text.Json.JsonSerializer.Serialize(credenciales);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/b1s/v1/Login", content);

            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);

            var data = await response.Content.ReadFromJsonAsync<LoginResponse>();
            _sessionId = data!.SessionId;
            _sessionTimeout = data.SessionTimeout;
            _version = data.Version;
            // Para saber cuando caduca la sesión
            _loginAt = DateTimeOffset.UtcNow;

            //_client.DefaultRequestHeaders.Add("Cookie", $"B1SESSION={_sessionId}");
        }

        public async Task Logout()
        {
            try
            {
                var response = await _client.PostAsync("/b1s/v1/Logout", null);
                if (!response.IsSuccessStatusCode)
                    throw await B1Exception.FromResponseAsync(response);
            }
            finally
            {
                foreach (Cookie c in _cookies.GetCookies(_client.BaseAddress!))
                {
                    c.Expired = true;
                }
                _sessionId = null;
                _loginAt = default;
            }
        }

        public bool IsExpired(TimeSpan? safetyMaring = null)
        {
            if (_sessionId is null) return true;
            var margin = safetyMaring ?? TimeSpan.FromSeconds(30);
            return DateTimeOffset.UtcNow > _loginAt.AddMinutes(_sessionTimeout) - margin;
        }


        public HttpClient HttpClient => _client;
    }
}