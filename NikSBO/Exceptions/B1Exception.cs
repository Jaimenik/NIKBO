using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace NikSBO.Exceptions
{
    /// <summary>
    /// Excepción que representa un error devuelto por el Service Layer de SAP Business One.
    /// Envuelve tanto el código HTTP como el código de error interno de SAP y su mensaje legible.
    /// </summary>
    public class B1Exception : Exception
    {
        /// <summary>
        /// Código de error interno de SAP (campo <c>error.code</c> del JSON de respuesta).
        /// Valdrá 0 si no se ha podido parsear el cuerpo como error OData estándar.
        /// </summary>
        public int SapErrorCode { get; }

        /// <summary>
        /// Código HTTP devuelto por el Service Layer (401, 404, 500...).
        /// </summary>
        public HttpStatusCode HttpStatusCode { get; }

        /// <summary>
        /// Crea una excepción con el código SAP, el mensaje y el estado HTTP.
        /// Normalmente se construye mediante <see cref="FromResponseAsync"/>.
        /// </summary>
        public B1Exception(int sapErrorCode, string message, HttpStatusCode httpStatusCode)
            : base(message)
        {
            SapErrorCode = sapErrorCode;
            HttpStatusCode = httpStatusCode;
        }

        /// <summary>
        /// Construye una <see cref="B1Exception"/> a partir de una respuesta HTTP fallida.
        /// Intenta leer el formato de error OData de SAP (<c>{ "error": { "code": ..., "message": { "value": ... } } }</c>);
        /// si el cuerpo no tiene ese formato, se devuelve una excepción con el cuerpo crudo y código 0,
        /// para no perder la información aunque el endpoint haya respondido algo inesperado.
        /// </summary>
        public static async Task<B1Exception> FromResponseAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(body);
                var error = doc.RootElement.GetProperty("error");
                var code = error.GetProperty("code").GetInt32();
                var message = error.GetProperty("message").GetProperty("value").GetString();

                return new B1Exception(code, message ?? body, response.StatusCode);
            }
            catch
            {
                return new B1Exception(0, body, response.StatusCode);
            }
        }

        /// <summary>
        /// Formato legible para logs: <c>SAP B1 Error [código] (HTTP xxx): mensaje</c>.
        /// </summary>
        public override string ToString() =>
            $"SAP B1 Error [{SapErrorCode}] (HTTP {(int)HttpStatusCode}): {Message}";
    }
}
