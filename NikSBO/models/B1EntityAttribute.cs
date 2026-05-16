using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NikSBO.models
{
    /// <summary>
    /// Atributo que asocia una clase del modelo con su endpoint del Service Layer.
    /// Lo usa <see cref="NikSBO.http.B1Client.Query{T}"/> para resolver la URL
    /// sin tener que pasar el nombre del recurso en cada llamada.
    /// <example>
    /// <code>
    /// [B1Entity("BusinessPartners")]
    /// public class BusinessPartner { ... }
    /// </code>
    /// </example>
    /// </summary>
    public class B1EntityAttribute : Attribute
    {
        /// <summary>
        /// Nombre del recurso en el Service Layer (ej. <c>"BusinessPartners"</c>, <c>"Orders"</c>).
        /// Se concatena al prefijo <c>b1s/v1/</c> para formar la URL final.
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// Marca la clase con el endpoint indicado.
        /// </summary>
        /// <param name="ep">Nombre del recurso del Service Layer.</param>
        public B1EntityAttribute(string ep) {
            this.Endpoint = ep;

        }
    }
}
