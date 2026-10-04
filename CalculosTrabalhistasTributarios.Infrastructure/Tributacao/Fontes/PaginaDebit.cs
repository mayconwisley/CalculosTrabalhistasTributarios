using HtmlAgilityPack;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

internal static class PaginaDebit
{
    /// <summary>A página traz o histórico completo, cada tabela com a vigência no título acima dela; devolve a mais recente.</summary>
    public static async Task<(DateOnly Competencia, string[][] Linhas)> ObterTabelaMaisRecenteAsync(HttpClient httpClient, Uri endereco, Regex vigencia, CancellationToken cancellationToken)
    {
        var documento = await LeituraDePagina.ObterDocumentoAsync(httpClient, endereco, cancellationToken);
        var maisRecente = (documento.DocumentNode.SelectNodes("//table") ?? Enumerable.Empty<HtmlNode>())
            .Select(tabela => (Tabela: tabela, Titulo: vigencia.Match(LeituraDePagina.Normalizar(tabela.SelectSingleNode("preceding::h2[1]")?.InnerText ?? string.Empty))))
            .Where(item => item.Titulo.Success)
            .Select(item => (Competencia: Competencia(item.Titulo), item.Tabela))
            .Where(item => item.Competencia is not null)
            .OrderByDescending(item => item.Competencia)
            .FirstOrDefault();

        if (maisRecente.Tabela is null)
            throw new InvalidOperationException("Nenhuma tabela com vigência foi encontrada na página.");

        return (maisRecente.Competencia!.Value, LeituraDePagina.LerLinhas(maisRecente.Tabela));
    }

    private static DateOnly? Competencia(Match titulo)
    {
        var mes = int.Parse(titulo.Groups["mes"].Value, CultureInfo.InvariantCulture);
        return mes is >= 1 and <= 12 ? new DateOnly(int.Parse(titulo.Groups["ano"].Value, CultureInfo.InvariantCulture), mes, 1) : null;
    }
}
