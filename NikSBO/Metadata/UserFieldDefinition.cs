using System.Collections.Generic;
using System.Text.Json.Serialization;
using NikSBO.Enums;

namespace NikSBO.Metadata
{
    /// <summary>
    /// Definición de un campo de usuario (UDF) para crearlo vía <c>UserFieldsMD</c>.
    /// Los UDFs llevan prefijo <c>U_</c> en la columna real, pero aquí el nombre va sin él
    /// (SAP lo añade automáticamente al crear).
    /// <para>
    /// Las propiedades opcionales se omiten del JSON cuando son <c>null</c> para evitar que SAP
    /// rechace campos que en algunas versiones no reconoce.
    /// </para>
    /// </summary>
    public class UserFieldDefinition
    {
        /// <summary>
        /// Tabla a la que se añade el campo. Tablas estándar de SAP sin prefijo (ej. <c>"OCRD"</c>),
        /// UDTs con prefijo <c>@</c> (ej. <c>"@ROUTES"</c>).
        /// </summary>
        public string TableName { get; set; } = "";

        /// <summary>Nombre del campo sin el prefijo <c>U_</c>. Ej. <c>"ROUTE"</c> → en BD será <c>U_ROUTE</c>.</summary>
        public string Name { get; set; } = "";

        /// <summary>Descripción visible. Ej. <c>"Código de ruta"</c>.</summary>
        public string Description { get; set; } = "";

        /// <summary>Tipo de dato del campo. Determina cómo SAP lo almacena y muestra.</summary>
        public UserFieldType Type { get; set; } = UserFieldType.Alphanumeric;

        /// <summary>Subtipo del campo (Address, Phone, Image, Price, etc.).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public UserFieldSubType? SubType { get; set; }

        /// <summary>Longitud máxima del campo. Solo aplica a <see cref="UserFieldType.Alphanumeric"/> y <see cref="UserFieldType.Numeric"/>.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Size { get; set; }

        /// <summary>Tamaño con el que SAP muestra el campo en formularios. Si null, usa el mismo que <see cref="Size"/>.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? EditSize { get; set; }

        /// <summary>Si <c>true</c>, el campo es obligatorio en formularios.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Mandatory { get; set; }

        /// <summary>Valor por defecto que se asigna al crear un registro.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DefaultValue { get; set; }

        /// <summary>Tabla a la que enlaza este campo (foreign key). Ej. <c>"OCRD"</c> para enlazar con socios de negocio.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LinkedTable { get; set; }

        /// <summary>Código del UDO al que enlaza este campo (si es FK a un UDO).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LinkedUDO { get; set; }

        /// <summary>
        /// ID del objeto de sistema al que enlaza este campo. Ej. <c>2</c> = BusinessPartner, <c>4</c> = Item.
        /// Útil para campos tipo "selector de cliente / artículo".
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LinkedSystemObject { get; set; }

        /// <summary>
        /// Lista de valores válidos para combos / dropdowns. SAP lo expone como <c>ValidValuesMD</c> en JSON,
        /// el <see cref="JsonPropertyNameAttribute"/> lo mapea automáticamente.
        /// </summary>
        [JsonPropertyName("ValidValuesMD")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ValidValue>? ValidValues { get; set; }
    }

    /// <summary>
    /// Valor válido para un campo con lista cerrada (combo). Ej. <c>("A", "Activo")</c>.
    /// </summary>
    /// <param name="Value">Valor que se almacena en BD (corto).</param>
    /// <param name="Description">Texto que ve el usuario en el combo.</param>
    public record ValidValue(string Value, string Description);
}
