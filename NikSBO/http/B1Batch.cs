using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NikSBO.Exceptions;
#if NETSTANDARD2_0
using NikSBO.Compat;   // polyfill: ReadAsStringAsync(CancellationToken) no existe en netstandard2.0
#endif

namespace NikSBO.http
{
    /// <summary>
    /// Acumula varias operaciones (POST/PATCH/DELETE) y las envía juntas como una transacción
    /// atómica al endpoint <c>$batch</c> del Service Layer. SAP envuelve las operaciones en un
    /// changeset, así que si una falla todas las demás se revierten.
    /// </summary>
    public class B1Batch
    {
        private B1Client _b1;

        /// <summary>Operación pendiente dentro de un batch.</summary>
        /// <param name="Method">Verbo HTTP: <c>POST</c>, <c>PATCH</c> o <c>DELETE</c>.</param>
        /// <param name="Endpoint">Endpoint relativo (sin el prefijo <c>b1s/v1/</c>).</param>
        /// <param name="Body">Cuerpo a serializar a JSON, o <c>null</c> para DELETE.</param>
        public record BatchOperation(string Method, string Endpoint, object? Body);
        private List<BatchOperation> _operations = new List<BatchOperation>();

        /// <summary>
        /// Resultado de una operación dentro de un batch. <see cref="Body"/> es el JSON crudo que
        /// devolvió SAP para esa operación (la entidad creada, el error, etc.) o null si no hubo cuerpo.
        /// </summary>
        /// <param name="StatusCode">Código HTTP que SAP devolvió para esta operación.</param>
        /// <param name="Body">JSON crudo del cuerpo de respuesta, o <c>null</c> si SAP no devolvió cuerpo.</param>
        public record BatchResult(int StatusCode, string? Body)
        {
            /// <summary>Atajo: <c>true</c> si <see cref="StatusCode"/> está en el rango 200-299.</summary>
            public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
        }

        /// <summary>
        /// Crea un nuevo batch ligado a un <see cref="B1Client"/>. Lo normal es instanciarlo
        /// vía <see cref="B1Client.CreateBatch"/>.
        /// </summary>
        /// <param name="b1">Cliente B1 ya autenticado por el que se enviará el batch.</param>
        public B1Batch(B1Client b1)
        {
            this._b1 = b1;

        }

        /// <summary>Encola un POST contra el endpoint indicado.</summary>
        /// <param name="endpoint">Endpoint relativo, ej. <c>"BusinessPartners"</c>.</param>
        /// <param name="body">Cuerpo a serializar a JSON.</param>
        public void Post(string endpoint, object body)
        {
            _operations.Add(new BatchOperation("POST", endpoint, body));
        }

        /// <summary>Encola un PATCH (actualización parcial) contra el endpoint indicado.</summary>
        /// <param name="endpoint">Endpoint relativo con la clave, ej. <c>"BusinessPartners('C001')"</c>.</param>
        /// <param name="body">Cuerpo a serializar a JSON con los campos a actualizar.</param>
        public void Patch(string endpoint, object body)
        {
            _operations.Add(new BatchOperation("PATCH", endpoint, body));
        }

        /// <summary>Encola un DELETE contra el endpoint indicado.</summary>
        /// <param name="endpoint">Endpoint relativo con la clave a borrar, ej. <c>"BusinessPartners('C001')"</c>.</param>
        public void Delete(string endpoint)
        {
            _operations.Add(new BatchOperation("DELETE", endpoint, null));
        }

        /// <summary>
        /// Envía el batch al Service Layer y devuelve el resultado de cada operación en el mismo orden
        /// en que se añadieron. Recuerda: las operaciones van dentro de un changeset, así que SAP las
        /// trata como una transacción atómica — si una falla, las demás se revierten.
        /// </summary>
        /// <param name="cancellationToken">Token para cancelar el envío del batch.</param>
        public async Task<List<BatchResult>> SubmitAsync(CancellationToken cancellationToken = default)
        {
            var sb = new StringBuilder();

            // Abre batch
            sb.Append("--batch_boundary\r\n");
            sb.Append("Content-Type: multipart/mixed; boundary=changeset_boundary\r\n");
            sb.Append("\r\n");

            // Operaciones
            foreach (var op in _operations)
            {
                sb.Append("--changeset_boundary\r\n");
                sb.Append("Content-Type: application/http\r\n");
                sb.Append("Content-Transfer-Encoding: binary\r\n");
                sb.Append("\r\n");
                sb.Append($"{op.Method} /b1s/v1/{op.Endpoint} HTTP/1.1\r\n");
                sb.Append("Content-Type: application/json\r\n");
                sb.Append("\r\n");

                if (op.Body != null)
                    sb.Append(System.Text.Json.JsonSerializer.Serialize(op.Body) + "\r\n");

                sb.Append("\r\n");
            }

            // Cierra changeset y batch
            sb.Append("--changeset_boundary--\r\n");
            sb.Append("--batch_boundary--\r\n");

            var bodyText = sb.ToString();

            var response = await _b1.ExecuteAsync((http, ct) =>
            {
                var content = new StringContent(bodyText, Encoding.UTF8);
                content.Headers.Remove("Content-Type");
                content.Headers.TryAddWithoutValidation("Content-Type", "multipart/mixed; boundary=batch_boundary");
                return http.PostAsync("/b1s/v1/$batch", content, ct);
            }, cancellationToken);

            // Si el sobre exterior peta (auth, server caído…), hay que tirar excepción —
            // ya no hay un multipart útil dentro.
            if (!response.IsSuccessStatusCode)
                throw await B1Exception.FromResponseAsync(response);

            var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

            return ParseBatchResponse(response.Content.Headers.ContentType?.Parameters
                                          .FirstOrDefault(p => p.Name == "boundary")?.Value,
                                      rawResponse);
        }

        /// <summary>
        /// Parsea la respuesta multipart de SAP en una lista de resultados, uno por operación.
        /// El formato tiene tres niveles: sobre exterior → changeset → mini-respuesta HTTP por operación.
        /// </summary>
        private static List<BatchResult> ParseBatchResponse(string? outerBoundary, string raw)
        {
            if (string.IsNullOrEmpty(outerBoundary))
                throw new InvalidOperationException("La respuesta del batch no incluye boundary en el Content-Type.");

            // Nivel 1: trocea el sobre exterior por el boundary que SAP eligió.
            // Filtra preámbulo, epílogo ("--<boundary>--") y trozos vacíos.
            var outerParts = raw
                .Split(new[] { "--" + outerBoundary }, StringSplitOptions.None)
                .Where(p => !string.IsNullOrWhiteSpace(p) && !p.TrimStart().StartsWith("--"))
                .ToList();

            var results = new List<BatchResult>();

            // Para cada parte del sobre exterior puede haber dos formatos:
            //  - Changeset (varias operaciones OK): trae su propio boundary y hay que recurrir.
            //  - Operación directa (típico cuando SAP aplana tras un rollback): es ya la mini-respuesta HTTP.
            foreach (var outerPart in outerParts)
            {
                var match = Regex.Match(outerPart, @"boundary=([^\s;\r\n]+)", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    // Nivel 2: trocea el changeset, una entrada por operación.
                    var changesetBoundary = match.Groups[1].Value.Trim('"');
                    var operationParts = outerPart
                        .Split(new[] { "--" + changesetBoundary }, StringSplitOptions.None)
                        .Where(p => !string.IsNullOrWhiteSpace(p) && !p.TrimStart().StartsWith("--"))
                        .ToList();

                    foreach (var opPart in operationParts)
                    {
                        var parsed = ParseOperation(opPart);
                        if (parsed is not null) results.Add(parsed);
                    }
                }
                else
                {
                    // SAP aplanó: el outerPart ya es directamente la operación.
                    var parsed = ParseOperation(outerPart);
                    if (parsed is not null) results.Add(parsed);
                }
            }

            return results;
        }

        /// <summary>
        /// Parsea la mini-respuesta HTTP de una operación: status line + headers + body JSON.
        /// Devuelve null si el trozo recibido no contiene una respuesta HTTP — esto pasa con
        /// el preámbulo del changeset (los headers que van antes del primer boundary interno).
        /// </summary>
        private static BatchResult? ParseOperation(string opPart)
        {
            var lines = opPart.Split(new[] { "\r\n" }, StringSplitOptions.None);

            // Encuentra la línea "HTTP/1.1 NNN Reason" — marca el inicio de la respuesta empotrada.
            var statusIdx = Array.FindIndex(lines, l => l.StartsWith("HTTP/", StringComparison.Ordinal));
            if (statusIdx < 0)
                return null;

            var statusParts = lines[statusIdx].Split(' ');
            var statusCode = statusParts.Length >= 2 && int.TryParse(statusParts[1], out var sc) ? sc : 0;

            // El cuerpo empieza tras la primera línea en blanco que sigue al status.
            var blankIdx = Array.IndexOf(lines, "", statusIdx);
            if (blankIdx < 0 || blankIdx + 1 >= lines.Length)
                return new BatchResult(statusCode, null);

            var body = string.Join("\r\n", lines.Skip(blankIdx + 1)).Trim();
            return new BatchResult(statusCode, string.IsNullOrEmpty(body) ? null : body);
        }
    }
}
