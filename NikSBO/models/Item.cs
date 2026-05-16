namespace NikSBO.models
{
    /// <summary>
    /// Artículo del maestro de SAP B1 (tabla OITM). Se mapea al recurso
    /// <c>Items</c> del Service Layer.
    /// <para>
    /// La mayoría de campos van nullable porque SAP puede no informarlos según el
    /// tipo de artículo (servicios, mano de obra, etc.) y, si no se marca nullable,
    /// la deserialización peta al recibir <c>null</c>.
    /// </para>
    /// <para>
    /// Hereda de <see cref="B1Model"/>, por lo que admite UDFs vía <c>GetUDF&lt;T&gt;</c>
    /// sin necesidad de declarar manualmente cada campo <c>U_*</c>.
    /// </para>
    /// </summary>
    [B1Entity("Items")]
    public class Item : B1Model
    {
        /// <summary>Número de artículo (clave primaria). Ej. <c>A00001</c>.</summary>
        public string ItemCode { get; set; }

        /// <summary>Descripción del artículo.</summary>
        public string? ItemName { get; set; }

        /// <summary>Nombre extranjero / descripción en otro idioma.</summary>
        public string? ForeignName { get; set; }

        /// <summary>Clase de artículo: <c>itItems</c>, <c>itLabor</c>, <c>itTravel</c>, <c>itFixedAssets</c>.</summary>
        public string? ItemType { get; set; }

        /// <summary>Código del grupo de artículos.</summary>
        public int? ItemsGroupCode { get; set; }

        /// <summary>Entrada del grupo de unidades de medida.</summary>
        public int? UoMGroupEntry { get; set; }

        /// <summary>Código de barras del artículo.</summary>
        public string? BarCode { get; set; }

        /// <summary>Lista de precios asociada al artículo.</summary>
        public int? PriceList { get; set; }

        /// <summary>Indica si es artículo de inventario: <c>tYES</c>/<c>tNO</c>.</summary>
        public string? InventoryItem { get; set; }

        /// <summary>Indica si es artículo de venta: <c>tYES</c>/<c>tNO</c>.</summary>
        public string? SalesItem { get; set; }

        /// <summary>Indica si es artículo de compra: <c>tYES</c>/<c>tNO</c>.</summary>
        public string? PurchaseItem { get; set; }

        /// <summary>Código del fabricante.</summary>
        public int? Manufacturer { get; set; }

        /// <summary>Activo/Inactivo: <c>tYES</c>/<c>tNO</c>.</summary>
        public string? Valid { get; set; }
    }
}
