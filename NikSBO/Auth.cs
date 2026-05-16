using NikSBO.Exceptions;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace NikSBO
{
    /// <summary>Cuerpo de la petición de login al endpoint <c>/b1s/v1/Login</c> del Service Layer.</summary>
    /// <param name="UserName">Usuario de SAP B1.</param>
    /// <param name="Password">Contraseña del usuario.</param>
    /// <param name="CompanyDB">Base de datos de la empresa.</param>
    public record LoginRequest(string UserName, string Password, string CompanyDB);

    /// <summary>Respuesta del Service Layer tras un login correcto.</summary>
    /// <param name="SessionId">Identificador de sesión que SAP guarda como cookie <c>B1SESSION</c>.</param>
    /// <param name="SessionTimeout">Minutos de vigencia de la sesión antes de caducar (típicamente 30).</param>
    /// <param name="Version">Versión del Service Layer.</param>
    public record LoginResponse(string SessionId, int SessionTimeout, string Version);


    /// <summary>
    /// Maneja el ciclo de vida de la sesión contra el Service Layer: login, logout y comprobación
    /// de expiración. <see cref="NikSBO.http.B1Client"/> lo usa internamente — no suele ser
    /// necesario instanciarlo a mano.
    /// </summary>
    public class Auth
    {
        private readonly CookieContainer _cookies;
        private readonly HttpClientHandler _handler;
        private readonly HttpClient _client;
        private string? _sessionId;
        private int _sessionTimeout;
        private string? _version;

        /// <summary>Duración de la sesión en minutos tal y como la reportó el Service Layer en el login.</summary>
        public int SessionTimeoutMinutes => _sessionTimeout;

        /// <summary>Versión del Service Layer, reportada en la respuesta del login.</summary>
        public string? Version => _version;

        private DateTimeOffset _loginAt;

        /// <summary>
        /// Momento UTC en el que se prevé que caduque la sesión actual, o <c>null</c> si todavía
        /// no se ha hecho login.
        /// </summary>
        public DateTimeOffset? ExpiresAt =>
                 _sessionId is null ? null : _loginAt.AddMinutes(_sessionTimeout);


        /// <summary>
        /// Crea la infraestructura HTTP (cookie container, handler que ignora el certificado y
        /// <see cref="HttpClient"/>) apuntando a la URL base del Service Layer.
        /// </summary>
        /// <param name="uri">URL base del Service Layer (incluyendo esquema y puerto).</param>
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

        /// <summary>
        /// Hace POST a <c>/b1s/v1/Login</c> y guarda el <c>SessionId</c>, <c>SessionTimeout</c> y
        /// <c>Version</c> devueltos. Lanza <see cref="B1Exception"/> si el SL responde con error.
        /// </summary>
        /// <param name="username">Usuario.</param>
        /// <param name="password">Contraseña.</param>
        /// <param name="companyDB">Base de datos de la empresa.</param>
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

        /// <summary>
        /// Hace POST a <c>/b1s/v1/Logout</c> para cerrar la sesión en el servidor y limpia las
        /// cookies locales. Aunque el servidor responda con error, las cookies y el estado de
        /// sesión local se descartan igualmente.
        /// </summary>
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

        /// <summary>
        /// Indica si la sesión está caducada (o si todavía no se ha hecho login). Se aplica un
        /// margen de seguridad por defecto de 30 segundos para evitar carreras con la caducidad real.
        /// </summary>
        /// <param name="safetyMaring">Margen de seguridad a restar al tiempo de caducidad.</param>
        public bool IsExpired(TimeSpan? safetyMaring = null)
        {
            if (_sessionId is null) return true;
            var margin = safetyMaring ?? TimeSpan.FromSeconds(30);
            return DateTimeOffset.UtcNow > _loginAt.AddMinutes(_sessionTimeout) - margin;
        }


        /// <summary>
        /// Cliente HTTP subyacente, con las cookies de sesión inyectadas. Útil para emitir
        /// peticiones manuales que requieran la sesión activa.
        /// </summary>
        public HttpClient HttpClient => _client;
    }
}
