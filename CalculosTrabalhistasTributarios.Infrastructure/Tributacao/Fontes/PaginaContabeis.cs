using HtmlAgilityPack;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

internal static partial class PaginaContabeis
{
    // Título de cada tabela: "Referência Vigente: Janeiro / 2026" ou "Referência Janeiro / 2025".
    [GeneratedRegex(@"referencia(?:\s+vigente)?\s*:?\s*(?<mes>[a-z]+)\s*/\s*(?<ano>\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex Referencia { get; }

    /// <summary>
    /// Cada tabela é uma seção com a referência no título e uma lista por linha, em vez de &lt;table&gt;.
    /// A página traz a vigente e a anterior; devolve a mais recente.
    /// </summary>
    public static async Task<(DateOnly Competencia, string[][] Linhas)> ObterTabelaMaisRecenteAsync(HttpClient httpClient, Uri endereco, CancellationToken cancellationToken)
    {
        var documento = await LeituraDePagina.ObterDocumentoAsync(httpClient, endereco, cancellationToken);
        var maisRecente = (documento.DocumentNode.SelectNodes("//section[contains(@class,'tabela')]") ?? Enumerable.Empty<HtmlNode>())
            .Select(secao => (Secao: secao, Titulo: Referencia.Match(LeituraDePagina.Normalizar(secao.SelectSingleNode(".//h3")?.InnerText ?? string.Empty))))
            .Where(item => item.Titulo.Success)
            .Select(item => (Competencia: Competencia(item.Titulo), item.Secao))
            .Where(item => item.Competencia is not null)
            .OrderByDescending(item => item.Competencia)
            .FirstOrDefault();

        if (maisRecente.Secao is null)
            throw new InvalidOperationException("Nenhuma tabela com referência foi encontrada na página.");

        var linhas = (maisRecente.Secao.SelectNodes(".//ul[contains(@class,'itemList')]") ?? Enumerable.Empty<HtmlNode>())
            .Select(lista => LeituraDePagina.LerTextos(lista.SelectNodes("./li")))
            .ToArray();
        return (maisRecente.Competencia!.Value, linhas);
    }

    private static DateOnly? Competencia(Match titulo) =>
        LeituraDePagina.ObterMes(titulo.Groups["mes"].Value) is { } mes
            ? new DateOnly(int.Parse(titulo.Groups["ano"].Value, CultureInfo.InvariantCulture), mes, 1)
            : null;
}
