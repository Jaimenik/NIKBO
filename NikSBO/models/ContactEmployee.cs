using System;

namespace NikSBO.models
{
    /// <summary>
    /// Persona de contacto del socio de negocio (tabla OCPR). Se mapea al recurso
    /// <c>ContactEmployees</c> del Service Layer. <see cref="MarketingDocument.ContactPersonCode"/>
    /// referencia su <see cref="InternalCode"/>. Hereda de <see cref="B1Model"/>
    /// para soportar UDFs vía <c>GetUDF&lt;T&gt;</c>.
    /// </summary>
    [B1Entity("ContactEmployees")]
    public class ContactEmployee : B1Model
    {
        /// <summary>Código interno del contacto (clave primaria, <c>CntctCode</c> en OCPR).</summary>
        public int InternalCode { get; set; }

        /// <summary>Código del socio de negocio al que pertenece el contacto.</summary>
        public string? CardCode { get; set; }

        /// <summary>Nombre completo del contacto.</summary>
        public string? Name { get; set; }

        /// <summary>Cargo / puesto.</summary>
        public string? Position { get; set; }

        /// <summary>Nombre.</summary>
        public string? FirstName { get; set; }

        /// <summary>Segundo nombre.</summary>
        public string? MiddleName { get; set; }

        /// <summary>Apellidos.</summary>
        public string? LastName { get; set; }

        /// <summary>Teléfono 1.</summary>
        public string? Tel1 { get; set; }

        /// <summary>Teléfono 2.</summary>
        public string? Tel2 { get; set; }

        /// <summary>Móvil.</summary>
        public string? MobilePhone { get; set; }

        /// <summary>Fax.</summary>
        public string? Fax { get; set; }

        /// <summary>Correo electrónico.</summary>
        public string? E_Mail { get; set; }

        /// <summary>Fecha de nacimiento.</summary>
        public DateTime? DateOfBirth { get; set; }

        /// <summary>Activo: <c>tYES</c>/<c>tNO</c>.</summary>
        public string? Active { get; set; }

        /// <summary>Comentarios.</summary>
        public string? Remarks1 { get; set; }
    }
}
