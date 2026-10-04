using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Atualizacao;

/// <summary>
/// Último release publicado no GitHub, com o instalador e o SHA-256 dele; pré-lançamentos e rascunhos não aparecem
/// nessa consulta. O hash vem do próprio GitHub ou, em releases antigos, das notas publicadas com o instalador.
/// </summary>
public sealed partial class ConsultaVersaoGitHub(Func<HttpClient> criarHttpClient) : IConsultaVersaoPublicada
{
    public const string Repositorio = "mayconwisley/CalculosTrabalhistasTributarios";
    private static readonly Uri UltimoRelease = new($"https://api.github.com/repos/{Repositorio}/releases/latest");

    // "SHA-256 do instalador: `a54f...`", como o publicar-release.ps1 escreve nas notas.
    [GeneratedRegex(@"SHA-256 do instalador:\s*`(?<hash>[0-9a-fA-F]{64})`")]
    private static partial Regex HashNasNotas { get; }

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
            return Ler(json.RootElement);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return Erro.Indisponivel("Não foi possível consultar a versão mais recente.");
        }
    }

    public static Result<VersaoPublicada> Ler(JsonElement release)
    {
        var tag = release.GetProperty("tag_name").GetString() ?? string.Empty;
        if (!Uri.TryCreate(release.GetProperty("html_url").GetString(), UriKind.Absolute, out var pagina))
            return Erro.Indisponivel("O GitHub respondeu sem o endereço do release.");

        Uri? instalador = null;
        string? hash = null;
        if (release.TryGetProperty("assets", out var arquivos))
            foreach (var arquivo in arquivos.EnumerateArray())
            {
                var nome = arquivo.GetProperty("name").GetString() ?? string.Empty;
                if (!nome.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase)
                    || !Uri.TryCreate(arquivo.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var endereco))
                    continue;
                instalador = endereco;
                if (arquivo.TryGetProperty("digest", out var digest) && digest.GetString() is { } valor && valor.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    hash = valor["sha256:".Length..];
                break;
            }
        if (hash is null && release.TryGetProperty("body", out var notas) && HashNasNotas.Match(notas.GetString() ?? string.Empty) is { Success: true } achado)
            hash = achado.Groups["hash"].Value;

        return new VersaoPublicada(tag.TrimStart('v', 'V'), pagina, instalador, hash?.ToLowerInvariant());
    }
}
