using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using HtmlAgilityPack;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Seguro-desemprego do iDinheiro: uma tabela por ano, com o ano no título acima dela.</summary>
public sealed partial class FonteSeguroDesempregoIdinheiro : IFonteTabela<TabelaSeguroDesempregoPublicada>
{
    // Título de cada tabela: "Tabela do Seguro Desemprego 2026". O reajuste anual vale desde janeiro.
    [GeneratedRegex(@"seguro[\s-]desemprego\s+(?<ano>20\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex Ano { get; }

    public string Nome => "idinheiro.com.br";

    public bool Oficial => false;

    public Uri Endereco { get; } = new("https://www.idinheiro.com.br/tabelas/tabela-seguro-desemprego/");

    public async Task<TabelaSeguroDesempregoPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var documento = await LeituraDePagina.ObterDocumentoAsync(httpClient, Endereco, cancellationToken);
        var maisRecente = (documento.DocumentNode.SelectNodes("//table") ?? Enumerable.Empty<HtmlNode>())
            .Select(tabela => (Tabela: tabela, Titulo: Ano.Match(LeituraDePagina.Normalizar(Titulo(tabela)))))
            .Where(item => item.Titulo.Success)
            .Select(item => (Ano: int.Parse(item.Titulo.Groups["ano"].Value, CultureInfo.InvariantCulture), item.Tabela))
            .OrderByDescending(item => item.Ano)
            .FirstOrDefault();

        if (maisRecente.Tabela is null)
            throw new InvalidOperationException("Nenhuma tabela do seguro-desemprego com o ano foi encontrada na página.");

        return new TabelaSeguroDesempregoPublicada(new DateOnly(maisRecente.Ano, 1, 1), LeituraDePagina.LerFaixasSeguroDesemprego(LeituraDePagina.LerLinhas(maisRecente.Tabela)));
    }

    private static string Titulo(HtmlNode tabela) =>
        tabela.SelectSingleNode("preceding::*[self::h2 or self::h3 or self::h4 or self::h5][1]")?.InnerText ?? string.Empty;
}
