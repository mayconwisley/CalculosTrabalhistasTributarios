using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Infrastructure.Atualizacao;

/// <summary>Último release publicado no GitHub; pré-lançamentos e rascunhos não aparecem nessa consulta.</summary>
public sealed class ConsultaVersaoGitHub(Func<HttpClient> criarHttpClient) : IConsultaVersaoPublicada
{
    private static readonly Uri UltimoRelease = new("https://api.github.com/repos/mayconwisley/CalculosTrabalhistasTributarios/releases/latest");

    public async Task<Result<VersaoPublicada>> ConsultarAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = criarHttpClient();
            using var pedido = new HttpRequestMessage(HttpMethod.Get, UltimoRelease);
            pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var resposta = await httpClient.SendAsync(pedido, cancellationToken);
            resposta.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(cancellationToken));
            var tag = json.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
            var pagina = json.RootElement.GetProperty("html_url").GetString();
            return pagina is null || !Uri.TryCreate(pagina, UriKind.Absolute, out var endereco)
                ? Erro.Indisponivel("O GitHub respondeu sem o endereço do release.")
                : new VersaoPublicada(tag.TrimStart('v', 'V'), endereco);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return Erro.Indisponivel("Não foi possível consultar a versão mais recente.");
        }
    }
}
