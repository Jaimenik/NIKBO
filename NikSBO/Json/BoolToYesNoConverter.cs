using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NikSBO.Json
{
    /// <summary>
    /// Convierte <c>bool</c> al string que espera SAP B1 en su enum interno <c>BoYesNoEnum</c>:
    /// <c>true</c> → <c>"tYES"</c>, <c>false</c> → <c>"tNO"</c>. Al leer acepta tanto la forma
    /// larga (<c>"tYES"</c>/<c>"tNO"</c>) como la corta (<c>"Y"</c>/<c>"N"</c>) — SAP devuelve
    /// indistintamente según el endpoint.
    /// <para>
    /// Solo se aplica en propiedades que lo declaren explícitamente con
    /// <c>[JsonConverter(typeof(BoolToYesNoConverter))]</c>. Necesario en endpoints viejos
    /// como <c>UserObjectsMD</c> que no aceptan los <c>true</c>/<c>false</c> JSON estándar.
    /// </para>
    /// <para>
    /// El converter trata <c>bool</c>; el wrapping para <c>bool?</c> lo gestiona automáticamente
    /// <c>System.Text.Json</c> — cuando el valor es null no se invoca este código.
    /// </para>
    /// </summary>
    internal class BoolToYesNoConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = reader.GetString();
            return s == "tYES" || s == "Y";
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value ? "tYES" : "tNO");
        }
    }
}
