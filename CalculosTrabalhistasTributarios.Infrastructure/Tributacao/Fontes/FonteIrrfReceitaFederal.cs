using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using HtmlAgilityPack;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>
/// Página anual de tabelas da Receita Federal. É a única fonte que publica, além das faixas, o desconto simplificado,
/// a dedução por dependente e a redução mensal.
/// </summary>
public sealed partial class FonteIrrfReceitaFederal : IFonteTabela<TabelaIrrfPublicada>
{
    private const string CatalogoUrl = PaginaReceitaFederal.CatalogoUrl;
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    [GeneratedRegex(@"/tabelas/(?<ano>20\d{2})/?$", RegexOptions.IgnoreCase)]
    private static partial Regex AnoNaUrl { get; }

    [GeneratedRegex(@"0,\d{3,6}")]
    private static partial Regex MultiplicadorReducao { get; }

    // "A partir de janeiro de 2026", "A partir de fevereiro de 2024": o mês define a competência da tabela.
    [GeneratedRegex(@"a partir (?:de )?(?<mes>[a-z]+)(?: de)? (?<ano>20\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex VigenciaAPartirDe { get; }

    public string Nome => "Receita Federal";

    public bool Oficial => true;

    public Uri Endereco { get; } = new(CatalogoUrl);

    public async Task<TabelaIrrfPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var (fonte, documento) = await PaginaReceitaFederal.ObterPaginaMaisRecenteAsync(httpClient, Endereco, cancellationToken);
        return ExtrairTabela(documento, fonte);
    }

    private static TabelaIrrfPublicada ExtrairTabela(HtmlDocument documento, Uri fonte)
    {
        var titulo = (documento.DocumentNode.SelectNodes("//h2|//h3") ?? Enumerable.Empty<HtmlNode>())
            .FirstOrDefault(node => LeituraDePagina.Normalizar(node.InnerText).Contains("tabela de incidencia mensal", StringComparison.OrdinalIgnoreCase));
        var tabela = titulo?.SelectSingleNode("following::table[1]")
            ?? throw new InvalidOperationException("A tabela de incidência mensal não foi encontrada na página da Receita Federal.");

        var faixas = LeituraDePagina.LerFaixasIrrf(LeituraDePagina.LerLinhas(tabela));
        var conteudo = WebUtility.HtmlDecode(documento.DocumentNode.InnerText);
        var competencia = ExtrairCompetencia(titulo.ParentNode?.InnerText ?? documento.DocumentNode.InnerText, fonte);
        var dependente = ExtrairValorRotulo(conteudo, "Dedução mensal por dependente");
        var simplificado = ExtrairValorRotulo(conteudo, "Limite mensal de desconto simplificado");
        var reducoes = ExtrairReducoesMensais(documento);

        return new TabelaIrrfPublicada(competencia, faixas, dependente, simplificado, reducoes);
    }

    private static IReadOnlyList<ReducaoMensalPublicada> ExtrairReducoesMensais(HtmlDocument documento)
    {
        var titulo = (documento.DocumentNode.SelectNodes("//h2|//h3") ?? Enumerable.Empty<HtmlNode>())
            .FirstOrDefault(node => LeituraDePagina.Normalizar(node.InnerText).Contains("tabela de reducao mensal", StringComparison.OrdinalIgnoreCase));
        var tabela = titulo?.SelectSingleNode("following::table[1]")
            ?? throw new InvalidOperationException("A tabela de redução mensal não foi encontrada na página da Receita Federal.");

        var reducoes = LeituraDePagina.LerLinhas(tabela)
            .Where(colunas => colunas.Length >= 2)
            .Select((colunas, indice) => new ReducaoMensalPublicada(
                indice + 1,
                LeituraDePagina.ExtrairUltimoValor(colunas[0]),
                indice == 0 ? 0m : ExtrairMultiplicadorReducao(colunas[1]),
                LeituraDePagina.ExtrairPrimeiroValor(colunas[1])))
            .ToArray();

        if (reducoes.Length != 2 ||
            reducoes[0].LimiteRendimentos != 5_000m ||
            reducoes[0].ValorBase <= 0m ||
            reducoes[1].LimiteRendimentos != 7_350m ||
            reducoes[1].Multiplicador <= 0m ||
            reducoes[1].ValorBase <= 0m ||
            Math.Abs(reducoes[1].ValorBase - reducoes[1].Multiplicador * reducoes[1].LimiteRendimentos) > 0.01m)
            throw new InvalidOperationException("A página da Receita Federal retornou uma estrutura de redução mensal inválida.");

        return reducoes;
    }

    // Sem o mês por extenso, a vigência não é presumida em janeiro: uma tabela de meio de ano gravada em janeiro
    // substituiria a dos meses anteriores.
    private static DateOnly ExtrairCompetencia(string conteudo, Uri fonte)
    {
        var match = VigenciaAPartirDe.Match(LeituraDePagina.Normalizar(conteudo));
        if (!match.Success || LeituraDePagina.ObterMes(match.Groups["mes"].Value) is not { } mes || !int.TryParse(match.Groups["ano"].Value, out var ano))
            throw new InvalidOperationException($"Não foi possível identificar o mês de vigência da tabela da Receita Federal ({fonte.AbsolutePath}).");

        return new DateOnly(ano, mes, 1);
    }

    private static decimal ExtrairMultiplicadorReducao(string texto)
    {
        var match = MultiplicadorReducao.Match(texto);
        return match.Success ? decimal.Parse(match.Value, Cultura) : 0m;
    }

    private static decimal ExtrairValorRotulo(string conteudo, string rotulo)
    {
        var match = Regex.Match(conteudo, $@"{Regex.Escape(rotulo)}\s*:\s*(?:R\$\s*)?(?<valor>\d{{1,3}}(?:\.\d{{3}})*,\d{{2}})", RegexOptions.IgnoreCase);
        if (!match.Success)
            throw new InvalidOperationException($"O valor de '{rotulo}' não foi encontrado na página da Receita Federal.");

        return decimal.Parse(match.Groups["valor"].Value, Cultura);
    }
}
