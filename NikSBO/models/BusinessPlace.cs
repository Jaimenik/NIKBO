namespace NikSBO.models
{
    /// <summary>
    /// Sucursal multi-establecimiento (BPL). Se mapea al recurso <c>BusinessPlaces</c>
    /// del Service Layer. <see cref="MarketingDocument.BPL_IDAssignedToInvoice"/>
    /// referencia su <see cref="BPLID"/>. Hereda de <see cref="B1Model"/> para soportar
    /// UDFs vía <c>GetUDF&lt;T&gt;</c>.
    /// </summary>
    [B1Entity("BusinessPlaces")]
    public class BusinessPlace : B1Model
    {
        /// <summary>Identificador de la sucursal (clave primaria).</summary>
        public int BPLID { get; set; }

        /// <summary>Nombre de la sucursal.</summary>
        public string? BPLName { get; set; }

        /// <summary>Código de la sucursal.</summary>
        public string? BPLCode { get; set; }

        /// <summary>Desactivada: <c>tYES</c>/<c>tNO</c>.</summary>
        public string? DisableBPL { get; set; }

        /// <summary>Calle.</summary>
        public string? AddressStreet { get; set; }

        /// <summary>Código postal.</summary>
        public string? AddressZip { get; set; }

        /// <summary>Ciudad.</summary>
        public string? AddressCity { get; set; }

        /// <summary>Provincia / Estado.</summary>
        public string? AddressState { get; set; }

        /// <summary>País.</summary>
        public string? AddressCountry { get; set; }

        /// <summary>Bloque / Manzana.</summary>
        public string? AddressBlock { get; set; }

        /// <summary>Edificio.</summary>
        public string? AddressBuilding { get; set; }

        /// <summary>Dirección libre adicional.</summary>
        public string? AddressFreeText { get; set; }

        /// <summary>NIF / número de IVA de la sucursal.</summary>
        public string? VATRegistrationNumber { get; set; }
    }
}
