using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using HtmlAgilityPack;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Página oficial do INSS com a tabela dos segurados empregado, doméstico e trabalhador avulso.</summary>
public sealed partial class FonteInssGovBr : IFonteTabela<TabelaInssPublicada>
{
    // "Tabelas válidas a partir da competência janeiro de 2026": só a competência, nunca outra data da página.
    [GeneratedRegex(@"competencia\s+(?:de\s+)?(?<mes>[a-z]+)(?:\s+de)?\s+(?<ano>20\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex CompetenciaVigente { get; }

    public string Nome => "gov.br (INSS)";

    public bool Oficial => true;

    public Uri Endereco { get; } = new("https://www.gov.br/inss/pt-br/direitos-e-deveres/inscricao-e-contribuicao/tabela-de-contribuicao-mensal");

    public async Task<TabelaInssPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var documento = await LeituraDePagina.ObterDocumentoAsync(httpClient, Endereco, cancellationToken);
        var tabela = (documento.DocumentNode.SelectNodes("//table") ?? Enumerable.Empty<HtmlNode>())
            .FirstOrDefault(item => LeituraDePagina.Normalizar(item.InnerText).Contains("aliquota progressiva para fins de recolhimento ao inss", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("A tabela de INSS para empregados não foi encontrada na página.");

        return new TabelaInssPublicada(ExtrairCompetencia(documento.DocumentNode.InnerText), LeituraDePagina.LerFaixasInss(LeituraDePagina.LerLinhas(tabela)));
    }

    private static DateOnly ExtrairCompetencia(string conteudo)
    {
        var match = CompetenciaVigente.Match(LeituraDePagina.Normalizar(conteudo));
        if (!match.Success || LeituraDePagina.ObterMes(match.Groups["mes"].Value) is not { } mes || !int.TryParse(match.Groups["ano"].Value, out var ano))
            throw new InvalidOperationException("Não foi possível identificar a competência da tabela de INSS.");

        return new DateOnly(ano, mes, 1);
    }
}
