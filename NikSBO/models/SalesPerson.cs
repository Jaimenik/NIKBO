namespace NikSBO.models
{
    /// <summary>
    /// Vendedor / empleado de ventas (tabla OSLP). Se mapea al recurso
    /// <c>SalesPersons</c> del Service Layer. <see cref="MarketingDocument.SalesPersonCode"/>
    /// referencia su <see cref="SalesEmployeeCode"/>. Hereda de <see cref="B1Model"/>
    /// para soportar UDFs vía <c>GetUDF&lt;T&gt;</c>.
    /// </summary>
    [B1Entity("SalesPersons")]
    public class SalesPerson : B1Model
    {
        /// <summary>Código del vendedor (clave primaria).</summary>
        public int SalesEmployeeCode { get; set; }

        /// <summary>Nombre del vendedor.</summary>
        public string? SalesEmployeeName { get; set; }

        /// <summary>Activo: <c>tYES</c>/<c>tNO</c>.</summary>
        public string? Active { get; set; }

        /// <summary>Correo electrónico.</summary>
        public string? Email { get; set; }

        /// <summary>Teléfono fijo.</summary>
        public string? Telephone { get; set; }

        /// <summary>Móvil.</summary>
        public string? Mobile { get; set; }

        /// <summary>Porcentaje de comisión por defecto.</summary>
        public double? Commission { get; set; }

        /// <summary>Código del grupo de comisión.</summary>
        public int? CommissionGroup { get; set; }

        /// <summary>Código del manager del vendedor.</summary>
        public int? ManagerCode { get; set; }

        /// <summary>Sucursal (BPL) a la que pertenece el vendedor.</summary>
        public int? BPLId { get; set; }

        /// <summary>Departamento del vendedor.</summary>
        public int? Department { get; set; }

        /// <summary>Comentarios.</summary>
        public string? Remarks { get; set; }
    }
}
