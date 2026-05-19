using NikSBO.Enums;
using NikSBO.Exceptions;
using NikSBO.http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace NikSBO.Query
{
    /// <summary>
    /// Query builder fluido para consultas OData contra el Service Layer de SAP B1.
    /// Permite encadenar <see cref="Where(string, BOCondition, string)"/>,
    /// <see cref="Select(string[])"/> y <see cref="Top"/>, y ejecutar la consulta con
    /// <see cref="GetAsync"/>, que deserializa el array <c>value</c> de la
    /// respuesta OData a <see cref="List{T}"/>.
    /// </summary>
    /// <typeparam name="T">Tipo del modelo a consultar. Normalmente decorado con <see cref="NikSBO.models.B1EntityAttribute"/>.</typeparam>
    public class B1Query<T>
    {

        private B1Client _b1;
        private string endpoint;
        private List<string> wheres = new List<string>();
        private string[] selects;
        private int top;
        private string orderBy;


        /// <summary>
        /// Envoltorio de la respuesta OData de SAP, que siempre viene con la forma
        /// <c>{ "value": [ ... ] }</c> para consultas de colección.
        /// </summary>
        public record ODataResponse<T>(List<T> value);

        /// <summary>
        /// Crea un query builder para el endpoint indicado. Lo normal es instanciarlo
        /// indirectamente mediante <see cref="NikSBO.http.B1Client.Query{T}()"/>.
        /// </summary>
        /// <param name="b1">Cliente B1 ya autenticado. Las peticiones se canalizan por su flujo de auth (re-login automático y retry 401).</param>
        /// <param name="endpoint">Ruta relativa completa, ej. <c>b1s/v1/BusinessPartners</c>.</param>
        public B1Query(B1Client b1, string endpoint) {

            this._b1 = b1;
            this.endpoint = endpoint;

        }


        #region Sobrecargas del where
        /// <summary>
        /// Añade un filtro <c>$filter</c> con un valor de texto.
        /// El valor se encierra entre comillas simples, como exige OData para strings.
        /// Llamadas múltiples se combinan con <c>and</c>.
        /// </summary>
        public B1Query<T> Where(string field, BOCondition condition, string value)
        {
            wheres.Add($"{field} {GetODataOperator(condition)} '{value}'");
            return this;
        }


        /// <summary>
        /// Filtro tipado con expresión LINQ. Recomendado siempre que tengas un modelo
        /// (BusinessPartner, Item, etc.). Para tablas de usuario o nombres de campo
        /// dinámicos sigue habiendo las sobrecargas con string.
        /// </summary>
        public B1Query<T> Where(Expression<Func<T, bool>> predicate)
        {
            var translator = new ODataExpressionTranslator();
            var clause = translator.Translate(predicate.Body);
            wheres.Add(clause);
            return this;
        }

        /// <summary>
        /// Sobrecarga para valores numéricos. No se entrecomillan porque OData
        /// espera números sin comillas y ponerlas provoca el error "incompatible types".
        /// </summary>
        public B1Query<T> Where(string field, BOCondition condition, int value)
        {
            wheres.Add($"{field} {GetODataOperator(condition)} {value}");
            return this;
        }

        /// <summary>
        /// Atajo para filtros de igualdad de tipo string (el caso más común).
        /// </summary>
        public B1Query<T> Where(string field, string value)
        {
            return Where(field, BOCondition.Equals, value);
        }

        #endregion

        /// <summary>Ordena ascendentemente por el campo indicado (<c>$orderby field asc</c>).</summary>
        /// <param name="field">Nombre del campo tal como lo expone el Service Layer.</param>
        public B1Query<T> OrderBy(string field)
        {
            this.orderBy = $"{field} asc";
            return this;
        }

        /// <summary>Ordena descendentemente por el campo indicado (<c>$orderby field desc</c>).</summary>
        /// <param name="field">Nombre del campo tal como lo expone el Service Layer.</param>
        public B1Query<T> OrderByDesc(string field)
        {
            this.orderBy = $"{field} desc";
            return this;
        }


        /// <summary>
        /// Limita los campos devueltos mediante <c>$select</c>. Útil para reducir payload
        /// cuando los recursos de SAP tienen decenas de propiedades.
        /// </summary>
        /// <param name="fields">Nombres de propiedades tal cual los expone el Service Layer.</param>
        public B1Query<T> Select(params string[] fields)
        {
            selects = fields;
            return this;
        }

        /// <summary>
        /// Variante tipada de <see cref="Select(string[])"/>. Renombrar la propiedad en el
        /// modelo rompe en compilación en vez de en runtime. El tipo devuelto sigue siendo
        /// <typeparamref name="T"/>; los campos no incluidos llegarán como <c>null</c> / <c>default</c>.
        /// <para>Formas admitidas:</para>
        /// <list type="bullet">
        ///   <item><description>Una propiedad: <c>.Select(bp =&gt; bp.CardCode)</c></description></item>
        ///   <item><description>Varias propiedades por params: <c>.Select(bp =&gt; bp.CardCode, bp =&gt; bp.CardName)</c></description></item>
        ///   <item><description>Tipo anónimo: <c>.Select(bp =&gt; new { bp.CardCode, bp.CardName })</c></description></item>
        /// </list>
        /// </summary>
        /// <param name="selectors">Una o más lambdas; cada una puede apuntar a una propiedad o devolver un tipo anónimo con varias.</param>
        public B1Query<T> Select(params Expression<Func<T, object>>[] selectors)
        {
            selects = selectors.SelectMany(ExtractFieldNames).ToArray();
            return this;
        }

        /// <summary>
        /// Extrae los nombres de propiedad desde una lambda. Maneja accesos directos
        /// (<c>x =&gt; x.A</c>, incluyendo el <c>Convert(...)</c> que el compilador añade para
        /// value types) y tipos anónimos (<c>x =&gt; new { x.A, x.B }</c>, representados como
        /// <see cref="NewExpression"/>).
        /// </summary>
        private static IEnumerable<string> ExtractFieldNames(Expression<Func<T, object>> selector)
        {
            var body = Unwrap(selector.Body);

            if (body is NewExpression n)
            {
                foreach (var arg in n.Arguments)
                {
                    var inner = Unwrap(arg);
                    if (inner is MemberExpression me)
                        yield return me.Member.Name;
                    else
                        throw new ArgumentException(
                            $"En '{selector}' el elemento '{arg}' no es un acceso a propiedad simple.",
                            nameof(selector));
                }
                yield break;
            }

            if (body is MemberExpression m)
            {
                yield return m.Member.Name;
                yield break;
            }

            throw new ArgumentException(
                $"La expresión '{selector}' no es válida. Usa 'x => x.Campo' o 'x => new {{ x.A, x.B }}'.",
                nameof(selector));
        }

        /// <summary>Quita el <c>Convert(...)</c> que el compilador inserta para value types.</summary>
        private static Expression Unwrap(Expression e) =>
            e is UnaryExpression u && u.NodeType == ExpressionType.Convert ? u.Operand : e;

        /// <summary>
        /// Limita el número de registros devueltos (<c>$top</c>).
        /// </summary>
        public B1Query<T> Top(int top)
        {
            this.top = top;
            return this;

        }

        /// <summary>
        /// Construye la URL con los parámetros OData acumulados, hace la petición GET (máximo 20 registros)
        /// y devuelve la lista deserializada. Lanza <see cref="B1Exception"/> si SAP responde con error.
        /// </summary>
        public async Task<List<T>> GetAsync()
        {
            var response = await _b1.ExecuteAsync(http => http.GetAsync(BuildUrl(false)));
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);
            var data = (await response.Content.ReadFromJsonAsync<ODataResponse<T>>())!;
            return data.value;

        }
        /// <summary>
        /// Lo mismo que GetAsync pero devuelve todos los registros
        /// </summary>
        /// <returns></returns>
        public async Task<List<T>> GetAllAsync()
        {
            var response = await _b1.ExecuteAsync(http => http.GetAsync(BuildUrl(false)));

            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);


            var rawJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(rawJson);
            var items = doc.RootElement.GetProperty("value").Deserialize<List<T>>();

            while (doc.RootElement.TryGetProperty("odata.nextLink", out var nextLink))
            {
                var nextUrl = "b1s/v1/" + nextLink.GetString();
                response = await _b1.ExecuteAsync(http => http.GetAsync(nextUrl));

                if (!response.IsSuccessStatusCode)
                    throw await B1Exception.FromResponseAsync(response);

                rawJson = await response.Content.ReadAsStringAsync();
                doc = JsonDocument.Parse(rawJson);
                var pageItems = doc.RootElement.GetProperty("value").Deserialize<List<T>>();

                items.AddRange(pageItems);
            }

            return items;
            //return data.value;

        }


        /// <summary>
        /// Devuelve el número total de registros que cumplen los filtros usando <c>$count</c>.
        /// </summary>
        public async Task<int> CountAsync()
        {
            var response = await _b1.ExecuteAsync(http => http.GetAsync(BuildUrl(true)));
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);
            var raw = await response.Content.ReadAsStringAsync();
            return int.Parse(raw);
        }

        private string BuildUrl(bool count = false)
        {

            var url = count ? endpoint + "/$count" : endpoint;
            var queryParams = new List<string>();

            if (wheres.Count > 0)
                queryParams.Add("$filter=" + Uri.EscapeDataString(string.Join(" and ", wheres)));

            if (selects != null && selects.Length > 0)
                queryParams.Add("$select=" + Uri.EscapeDataString(string.Join(",", selects)));

            if (top > 0)
                queryParams.Add("$top=" + top);

            if (!string.IsNullOrEmpty(orderBy))
                queryParams.Add("$orderby=" + Uri.EscapeDataString(orderBy));

            if (queryParams.Count > 0)
                url = url + "?" + string.Join("&", queryParams);

            return url;
        }

        /// <summary>
        /// Traduce el <see cref="BOCondition"/> al operador textual de OData.
        /// </summary>
        private string GetODataOperator(BOCondition condition)
        {
            return condition switch
            {
                BOCondition.Equals => "eq",
                BOCondition.NotEquals => "ne",
                BOCondition.GreaterThan => "gt",
                BOCondition.LessThan => "lt",
                BOCondition.GreaterOrEqual => "ge",
                BOCondition.LessOrEqual => "le",
                _ => "eq"
            };
        }

    }
}
