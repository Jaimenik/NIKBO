using System.Text.Json.Serialization;
using NikSBO.Enums;

namespace NikSBO.Metadata
{
    /// <summary>
    /// Definición de una tabla de usuario (UDT) para crearla vía <c>UserTablesMD</c>.
    /// Las UDTs en SAP B1 llevan prefijo <c>@</c> en la base de datos; aquí el nombre va
    /// sin él (el SDK no lo añade — SAP acepta el nombre crudo en este endpoint).
    /// <para>
    /// Las propiedades opcionales se omiten del JSON cuando son <c>null</c> (vía
    /// <c>JsonIgnoreCondition.WhenWritingNull</c>) para evitar que SAP rechace campos
    /// que en algunas versiones no reconoce.
    /// </para>
    /// </summary>
    public class UserTableDefinition
    {
        /// <summary>Nombre de la tabla sin el prefijo <c>@</c>. Ej. <c>"ROUTES"</c>.</summary>
        public string TableName { get; set; } = "";

        /// <summary>Descripción visible para el usuario en SAP. Ej. <c>"Rutas de reparto"</c>.</summary>
        public string TableDescription { get; set; } = "";

        /// <summary>Tipo de tabla. <see cref="UserTableType.NoObject"/> para una UDT suelta sin UDO encima.</summary>
        public UserTableType TableType { get; set; } = UserTableType.NoObject;

        /// <summary>Si <c>true</c>, los registros pueden archivarse (mover a histórico).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Archivable { get; set; }

        /// <summary>Nombre del campo de fecha usado para decidir qué registros archivar. Solo aplica si <see cref="Archivable"/> es true.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ArchiveDateField { get; set; }
    }
}
