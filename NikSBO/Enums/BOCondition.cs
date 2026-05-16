using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NikSBO.Enums
{
    /// <summary>
    /// Operadores de comparación soportados por <see cref="NikSBO.Query.B1Query{T}.Where(string, BOCondition, string)"/>.
    /// Cada valor se traduce al operador OData correspondiente (<c>eq</c>, <c>ne</c>, <c>gt</c>...) al construir la URL.
    /// </summary>
    public enum BOCondition
    {
        /// <summary>Igual a (<c>eq</c>).</summary>
        Equals,
        /// <summary>Distinto de (<c>ne</c>).</summary>
        NotEquals,
        /// <summary>Mayor que (<c>gt</c>).</summary>
        GreaterThan,
        /// <summary>Menor que (<c>lt</c>).</summary>
        LessThan,
        /// <summary>Mayor o igual que (<c>ge</c>).</summary>
        GreaterOrEqual,
        /// <summary>Menor o igual que (<c>le</c>).</summary>
        LessOrEqual
    }
}
