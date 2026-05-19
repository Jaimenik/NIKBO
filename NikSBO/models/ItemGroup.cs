namespace NikSBO.models
{
    /// <summary>
    /// Grupo de artículos (tabla OITB). Se mapea al recurso <c>ItemGroups</c>
    /// del Service Layer. <see cref="Item.ItemsGroupCode"/> referencia su
    /// <see cref="Number"/>. Hereda de <see cref="B1Model"/> para soportar
    /// UDFs vía <c>GetUDF&lt;T&gt;</c>.
    /// </summary>
    [B1Entity("ItemGroups")]
    public class ItemGroup : B1Model
    {
        /// <summary>Código del grupo (clave primaria, <c>ItmsGrpCod</c> en OITB).</summary>
        public int Number { get; set; }

        /// <summary>Nombre del grupo.</summary>
        public string? GroupName { get; set; }

        /// <summary>Sistema de planificación: <c>bop_None</c> o <c>bop_MRP</c>.</summary>
        public string? PlanningSystem { get; set; }

        /// <summary>Método de aprovisionamiento: <c>bom_Make</c> o <c>bom_Buy</c>.</summary>
        public string? ProcurementMethod { get; set; }

        /// <summary>Almacén de componentes: <c>bwh_FromComponent</c> o <c>bwh_FromParent</c>.</summary>
        public string? ComponentWarehouse { get; set; }

        /// <summary>Tolerancia en porcentaje.</summary>
        public double? TolerancePercent { get; set; }

        /// <summary>Plazo de entrega en días.</summary>
        public int? LeadTime { get; set; }

        /// <summary>Intervalo de pedido en días.</summary>
        public int? OrderInterval { get; set; }

        /// <summary>Múltiplo de pedido.</summary>
        public int? OrderMultiple { get; set; }

        /// <summary>Cantidad mínima de pedido.</summary>
        public double? MinimumOrderQuantity { get; set; }
    }
}
