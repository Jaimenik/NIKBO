using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NikSBO.models
{
    /// <summary>
    /// Socio de negocio (cliente, proveedor o lead) de SAP B1.
    /// Se mapea al recurso <c>BusinessPartners</c> del Service Layer.
    /// Hereda de <see cref="B1Model"/> para soportar UDFs vía <c>GetUDF&lt;T&gt;</c>.
    /// </summary>
    [B1Entity("BusinessPartners")]
    public class BusinessPartner : B1Model
    {
        /// <summary>Código único del socio (clave primaria). Ej. <c>C30000</c>.</summary>
        public string CardCode { get; set; }

        /// <summary>Nombre comercial del socio.</summary>
        public string CardName { get; set; }

        /// <summary>Tipo de socio: <c>cCustomer</c>, <c>cSupplier</c> o <c>cLid</c>.</summary>
        public string CardType { get; set; }

        /// <summary>Correo electrónico principal del socio.</summary>
        public string EmailAddress { get; set; }


    }
}
