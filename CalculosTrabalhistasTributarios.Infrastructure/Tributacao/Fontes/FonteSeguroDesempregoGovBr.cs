using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using HtmlAgilityPack;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Página do Ministério do Trabalho sobre o seguro-desemprego formal, com a tabela de faixas do ano em vigor.</summary>
public sealed partial class FonteSeguroDesempregoGovBr : IFonteTabela<TabelaSeguroDesempregoPublicada>
{
    // "Esta tabela entra em vigor a partir do dia 11/01/2024": o reajuste vale desde janeiro.
    [GeneratedRegex(@"entra em vigor a partir do dia (?<dia>\d{1,2})/(?<mes>\d{1,2})/(?<ano>\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex Vigencia { get; }

    // "Período: Ano de 2024", antes da tabela, quando a página não traz a data de vigência.
    [GeneratedRegex(@"periodo:?\s*ano de (?<ano>\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex Periodo { get; }

    public string Nome => "gov.br (Ministério do Trabalho)";

    public bool Oficial => true;

    public Uri Endereco { get; } = new("https://www.gov.br/trabalho-e-emprego/pt-br/servicos/trabalhador/seguro-desemprego/seguro-desemprego-formal");

    public async Task<TabelaSeguroDesempregoPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var documento = await LeituraDePagina.ObterDocumentoAsync(httpClient, Endereco, cancellationToken);
        var faixas = (documento.DocumentNode.SelectNodes("//table") ?? Enumerable.Empty<HtmlNode>())
            .Select(tabela => LeituraDePagina.LerFaixasSeguroDesemprego(LeituraDePagina.LerLinhas(tabela)))
            .FirstOrDefault(faixas => faixas.Length > 0)
            ?? throw new InvalidOperationException("A tabela do seguro-desemprego não foi encontrada na página do Ministério do Trabalho.");
        return new TabelaSeguroDesempregoPublicada(Competencia(LeituraDePagina.Normalizar(documento.DocumentNode.InnerText)), faixas);
    }

    private static DateOnly Competencia(string texto)
    {
        if (Vigencia.Match(texto) is { Success: true } vigencia)
            return new DateOnly(int.Parse(vigencia.Groups["ano"].Value, CultureInfo.InvariantCulture), int.Parse(vigencia.Groups["mes"].Value, CultureInfo.InvariantCulture), 1);
        if (Periodo.Match(texto) is { Success: true } periodo)
            return new DateOnly(int.Parse(periodo.Groups["ano"].Value, CultureInfo.InvariantCulture), 1, 1);
        throw new InvalidOperationException("A vigência da tabela do seguro-desemprego não foi encontrada na página do Ministério do Trabalho.");
    }
}
