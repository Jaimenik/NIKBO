using System.Collections.Generic;
using System.Text.Json;

namespace NikSBO.models
{
    /// <summary>
    /// Clase base para todos los modelos mapeados al Service Layer de SAP B1.
    /// Centraliza el soporte de UDFs (<c>U_*</c>) capturándolos en
    /// <see cref="ExtensionData"/> y exponiéndolos vía <see cref="GetUDF{T}"/>,
    /// para que cada modelo concreto no tenga que repetir el mismo boilerplate.
    /// </summary>
    public class B1Model
    {
        /// <summary>
        /// Diccionario donde caen todas las propiedades del JSON que no tienen una
        /// propiedad fuerte declarada (típicamente UDFs <c>U_*</c>). Acceso recomendado
        /// vía <see cref="GetUDF{T}"/>.
        /// </summary>
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }

        /// <summary>
        /// Devuelve el valor de un UDF (<c>U_NOMBRE</c>) deserializado al tipo indicado,
        /// o <c>default</c> si el campo no viene en la respuesta.
        /// </summary>
        /// <typeparam name="T">Tipo al que deserializar el valor (string, int, decimal, DateTime...).</typeparam>
        /// <param name="name">Nombre del UDF tal como lo expone SAP (ej. <c>U_MY_FIELD</c>).</param>
        public T? GetUDF<T>(string name)
        {
            if (ExtensionData != null && ExtensionData.TryGetValue(name, out var value))
                return value.Deserialize<T>();
            return default;
        }
    }
}
