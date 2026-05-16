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
    }
}
