namespace NikSBO.models
{
    /// <summary>
    /// Almacén (tabla OWHS). Se mapea al recurso <c>Warehouses</c> del Service Layer.
    /// <see cref="DocumentLine.WarehouseCode"/> referencia su <see cref="WarehouseCode"/>.
    /// Hereda de <see cref="B1Model"/> para soportar UDFs vía <c>GetUDF&lt;T&gt;</c>.
    /// </summary>
    [B1Entity("Warehouses")]
    public class Warehouse : B1Model
    {
        /// <summary>Código del almacén (clave primaria, <c>WhsCode</c> en OWHS).</summary>
        public string WarehouseCode { get; set; }

        /// <summary>Nombre del almacén.</summary>
        public string? WarehouseName { get; set; }

        /// <summary>Calle.</summary>
        public string? Street { get; set; }

        /// <summary>Código postal.</summary>
        public string? Zip { get; set; }

        /// <summary>Ciudad.</summary>
        public string? City { get; set; }

        /// <summary>Provincia / Estado.</summary>
        public string? State { get; set; }

        /// <summary>País.</summary>
        public string? Country { get; set; }

        /// <summary>Sucursal (BPL) a la que pertenece el almacén.</summary>
        public int? BusinessPlaceID { get; set; }

        /// <summary>Inactivo: <c>tYES</c>/<c>tNO</c>.</summary>
        public string? Inactive { get; set; }

        /// <summary>Drop-shipping: <c>tYES</c>/<c>tNO</c>.</summary>
        public string? DropShip { get; set; }

        /// <summary>Ubicaciones (bins) habilitadas: <c>tYES</c>/<c>tNO</c>.</summary>
        public string? EnableBinLocations { get; set; }
    }
}
