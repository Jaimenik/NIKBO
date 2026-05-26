using System.Text.Json.Serialization;
using NikSBO.Enums;
using NikSBO.Json;

namespace NikSBO.Metadata
{
    /// <summary>
    /// Definición de un objeto de usuario (UDO) para crearlo vía <c>UserObjectsMD</c>.
    /// Un UDO da capacidades de CRUD, permisos, formularios y series numéricas a una UDT subyacente.
    /// <para>
    /// Nota: <c>UserObjectsMD</c> es un endpoint legacy que usa el enum interno <c>BoYesNoEnum</c>
    /// de SAP para booleanos. Por eso las propiedades <c>bool?</c> de este DTO se serializan con
    /// <see cref="BoolToYesNoConverter"/> a <c>"tYES"</c>/<c>"tNO"</c> en vez de <c>true</c>/<c>false</c>.
    /// </para>
    /// <para>
    /// Las propiedades opcionales se omiten del JSON cuando son <c>null</c> — SAP valida la
    /// existencia de cada propiedad enviada y rechaza nombres que no reconoce en su versión.
    /// </para>
    /// </summary>
    public class UserObjectDefinition
    {
        /// <summary>Identificador único del UDO (clave primaria). Ej. <c>"ROUTE_UDO"</c>.</summary>
        public string Code { get; set; } = "";

        /// <summary>Nombre visible del UDO. Ej. <c>"Rutas"</c>.</summary>
        public string Name { get; set; } = "";

        /// <summary>Tabla principal del UDO (la UDT base, sin el prefijo <c>@</c>). Ej. <c>"ROUTES"</c>.</summary>
        public string TableName { get; set; } = "";

        /// <summary>Tipo de objeto: master data o documento.</summary>
        public UserObjectType ObjectType { get; set; } = UserObjectType.MasterData;

        /// <summary>Habilita la operación de cancelación sobre el documento (solo aplica a <see cref="UserObjectType.Document"/>).</summary>
        [JsonConverter(typeof(BoolToYesNoConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? CanCancel { get; set; }

        /// <summary>Habilita la operación de cierre.</summary>
        [JsonConverter(typeof(BoolToYesNoConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? CanClose { get; set; }

        /// <summary>Si <c>true</c>, SAP genera un formulario por defecto cuando se accede al UDO desde el menú.</summary>
        [JsonConverter(typeof(BoolToYesNoConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? CanCreateDefaultForm { get; set; }

        /// <summary>Habilita el borrado de registros desde la UI.</summary>
        [JsonConverter(typeof(BoolToYesNoConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? CanDelete { get; set; }

        /// <summary>Habilita la operación de búsqueda en la UI.</summary>
        [JsonConverter(typeof(BoolToYesNoConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? CanFind { get; set; }

        /// <summary>Activa el log de cambios (auditoría) sobre los registros del UDO.</summary>
        [JsonConverter(typeof(BoolToYesNoConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? CanLog { get; set; }

        /// <summary>Permite el cierre de ejercicio (year transfer) sobre los datos del UDO.</summary>
        [JsonConverter(typeof(BoolToYesNoConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? CanYearTransfer { get; set; }

        /// <summary>Habilita series numéricas (numeración automática de registros).</summary>
        [JsonConverter(typeof(BoolToYesNoConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? ManageSeries { get; set; }

        /// <summary>Si <c>true</c>, el formulario del UDO es único (no se pueden abrir varias instancias).</summary>
        [JsonConverter(typeof(BoolToYesNoConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? UseUniqueFormType { get; set; }

        /// <summary>Activa el modo de formulario mejorado (más opciones de layout).</summary>
        [JsonConverter(typeof(BoolToYesNoConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? EnableEnhancedForm { get; set; }

        /// <summary>Número de columnas del formulario mejorado. Puede no estar soportada en todas las versiones de SAP B1.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? EnhancedFormColumns { get; set; }

        /// <summary>Número de filas del formulario mejorado. Puede no estar soportada en todas las versiones de SAP B1.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? EnhancedFormRows { get; set; }

        /// <summary>Texto del menú que abre el UDO.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MenuCaption { get; set; }

        /// <summary>ID del elemento de menú padre donde colgar este UDO. Default lo decide SAP.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MenuItem { get; set; }

        /// <summary>Número de columnas del formulario estándar (no enhanced).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? FormColumns { get; set; }

        /// <summary>Nombre de la extensión / add-on que registra el UDO (para integraciones con add-ons compilados).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ExtensionName { get; set; }
    }
}
