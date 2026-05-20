using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NikSBO.models
{
    /// <summary>
    /// Parámetros de conexión al Service Layer de SAP Business One.
    /// Se pasa al constructor de <see cref="NikSBO.http.B1Client"/> y queda guardado
    /// para los re-logins automáticos cuando expira la sesión.
    /// </summary>
    public class B1Options
    {
        /// <summary>URL base del Service Layer, ej. <c>https://sap.miempresa.local:50000/</c>.</summary>
        public string ServerUrl { get; set; }

        /// <summary>Identificador de la base de datos de la empresa (CompanyDB), ej. <c>SBODemoES</c>.</summary>
        public string CompanyDb { get; set; }

        /// <summary>Usuario de SAP B1 con permisos sobre la <see cref="CompanyDb"/>.</summary>
        public string Username { get; set; }

        /// <summary>Contraseña del usuario.</summary>
        public string Password { get; set; }

        /// <summary>
        /// Si <c>true</c>, acepta cualquier certificado TLS del Service Layer sin validarlo.
        /// Típico en SAP B1 on-prem con certificado autofirmado o vencido. Default: <c>false</c>
        /// (validación estricta de cadena de confianza).
        /// <para>
        /// <b>Sólo actívalo si la conexión va por una red de confianza (LAN corporativa, VPN).</b>
        /// Sobre Internet, WiFi pública o redes no segmentadas abre la puerta a ataques MITM:
        /// un atacante puede presentar su propio certificado, leer credenciales y modificar
        /// datos en tránsito.
        /// </para>
        /// </summary>
        public bool AcceptAnyServerCertificate { get; set; } = false;

        /// <summary>
        /// Hook de tracing para inspeccionar lo que el SDK hace por dentro: cada petición HTTP
        /// con método, ruta, status code y tiempo; eventos de login/logout; renovaciones de sesión
        /// automáticas; reintentos por 401; fallos de red. Si es <c>null</c> (default) no se loguea
        /// nada. Lo recibe el usuario como string ya formateado; no hay niveles ni contexto
        /// estructurado a propósito, para no forzar dependencias.
        /// <para>
        /// Ejemplo: <c>LogTrace = msg =&gt; Console.WriteLine($"[NikSBO] {msg}")</c>.
        /// Para integrarlo con <c>ILogger</c>: <c>LogTrace = msg =&gt; logger.LogDebug(msg)</c>.
        /// </para>
        /// </summary>
        public Action<string>? LogTrace { get; set; }
    }
}
