namespace NikSBO.models
{
    /// <summary>
    /// Grupo de IVA / código de impuesto (tabla OVTG). Se mapea al recurso
    /// <c>VatGroups</c> del Service Layer. <see cref="DocumentLine.TaxCode"/>
    /// referencia su <see cref="Code"/>. Hereda de <see cref="B1Model"/> para
    /// soportar UDFs vía <c>GetUDF&lt;T&gt;</c>.
    /// <para>
    /// Localizaciones US / Canada usan en su lugar los endpoints
    /// <c>SalesTaxCodes</c> y <c>PurchaseTaxCodes</c>.
    /// </para>
    /// </summary>
    [B1Entity("VatGroups")]
    public class VatGroup : B1Model
    {
        /// <summary>Código del grupo de IVA (clave primaria, ej. <c>IV01</c>).</summary>
        public string Code { get; set; }

        /// <summary>Nombre / descripción del grupo.</summary>
        public string? Name { get; set; }

        /// <summary>Categoría: <c>bovcInputTax</c> (compras) o <c>bovcOutputTax</c> (ventas).</summary>
        public string? Category { get; set; }

        /// <summary>Exento real: <c>tYES</c>/<c>tNO</c>.</summary>
        public string? TrueExempt { get; set; }

        /// <summary>Cuenta de impuesto asociada.</summary>
        public string? TaxAccount { get; set; }

        /// <summary>Cuenta de deducción.</summary>
        public string? DeductibleAccount { get; set; }
    }
}
