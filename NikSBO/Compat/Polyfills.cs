#if NETSTANDARD2_0
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// netstandard2.0 (y por tanto .NET Framework) no trae estas tres cosas que el resto del SDK usa.
// Las añadimos aquí como extensiones/polyfills para no contaminar el resto del código con #if.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill necesario para que el compilador permita propiedades con <c>init</c> y
    /// tipos <c>record</c> al compilar contra netstandard2.0.
    /// </summary>
    internal static class IsExternalInit { }
}

namespace NikSBO.Compat
{
    /// <summary>
    /// netstandard2.0 no expone <c>HttpClient.PatchAsync</c>. Esta extensión imita
    /// la API moderna usando <c>SendAsync</c> con un <see cref="HttpMethod"/> "PATCH".
    /// </summary>
    internal static class HttpClientCompatExtensions
    {
        public static Task<HttpResponseMessage> PatchAsync(
            this HttpClient client, string requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri) { Content = content };
            return client.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// netstandard2.0 solo expone <c>HttpContent.ReadAsStringAsync()</c> sin token.
    /// Esta extensión añade la sobrecarga moderna; el token se ignora porque la API
    /// subyacente no lo soporta — la cancelación se aplica en la petición HTTP previa.
    /// </summary>
    internal static class HttpContentCompatExtensions
    {
        public static Task<string> ReadAsStringAsync(this HttpContent content, CancellationToken cancellationToken)
            => content.ReadAsStringAsync();
    }
}
#endif
