using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Responde a cada site com uma página fixa, para ler as fontes sem depender da internet.</summary>
/// <param name="paginas">Página de cada host; sem o charset, os bytes vão como o servidor do Planalto os envia.</param>
internal sealed class PaginasFixas(IReadOnlyDictionary<string, (byte[] Conteudo, string? Charset)> paginas) : HttpMessageHandler
{
    public static HttpClient Cliente(params (string Host, string Html)[] paginas) =>
        new(new PaginasFixas(paginas.ToDictionary(pagina => pagina.Host, pagina => (Encoding.UTF8.GetBytes(pagina.Html), (string?)"utf-8"))));

    /// <summary>Página em Latin-1 sem o charset no cabeçalho, como as leis do Planalto.</summary>
    public static HttpClient ClienteLatin1(string host, string html) =>
        new(new PaginasFixas(new Dictionary<string, (byte[], string?)> { [host] = (Encoding.Latin1.GetBytes(html), null) }));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!paginas.TryGetValue(request.RequestUri!.Host, out var pagina))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        var conteudo = new ByteArrayContent(pagina.Conteudo);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("text/html") { CharSet = pagina.Charset };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = conteudo });
    }
}
