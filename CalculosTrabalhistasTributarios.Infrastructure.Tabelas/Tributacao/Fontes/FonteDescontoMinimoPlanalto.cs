using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using HtmlAgilityPack;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>
/// Texto compilado da Lei 9.430/1996 no Planalto. O art. 67 fixa o limite da dispensa de retenção do IRRF, e o art. 87,
/// o início dos efeitos da lei. Redações revogadas continuam na página, riscadas, e são ignoradas.
/// </summary>
public sealed partial class FonteDescontoMinimoPlanalto : IFonteTabela<DescontoMinimoPublicado>
{
    private const string Norma = "art. 67 da Lei 9.430/1996";

    // "Art. 67. Fica dispensada a retenção de imposto de renda, de valor igual ou inferior a R$ 10,00 (dez reais), ...".
    [GeneratedRegex(@"^Art\.\s*67\.\s.*?inferior a R\$\s*(?<valor>\d{1,3}(?:\.\d{3})*,\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex Artigo67 { get; }

    // "(Redação dada pela Lei nº 15.000, de 2027)": a página não diz desde quando vale a nova redação.
    [GeneratedRegex(@"Reda[cç][aã]o dada pel[ao] (?<norma>[^)]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RedacaoAlterada { get; }

    // Art. 87: "produzindo efeitos financeiros a partir de 1º de janeiro de 1997".
    [GeneratedRegex(@"efeitos financeiros a partir de \d{1,2}\S? de (?<mes>[a-z]+) de (?<ano>\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex InicioDosEfeitos { get; }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacos { get; }

    public string Nome => "Planalto (Lei 9.430/1996)";

    public bool Oficial => true;

    public Uri Endereco { get; } = new("https://www.planalto.gov.br/ccivil_03/leis/l9430.htm");

    public async Task<DescontoMinimoPublicado> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var documento = await LeituraDePagina.ObterDocumentoAsync(httpClient, Endereco, cancellationToken);
        var paragrafos = (documento.DocumentNode.SelectNodes("//p") ?? Enumerable.Empty<HtmlNode>()).Select(TextoEmVigor).ToArray();

        var artigo = paragrafos.Select(texto => (Texto: texto, Valor: Artigo67.Match(texto))).FirstOrDefault(item => item.Valor.Success);
        if (artigo.Texto is null)
            throw new InvalidOperationException($"O {Norma} não foi encontrado na página do Planalto.");
        if (RedacaoAlterada.Match(artigo.Texto) is { Success: true } alteracao)
            throw new InvalidOperationException($"O {Norma} tem nova redação ({alteracao.Groups["norma"].Value.Trim()}). Confira a vigência do novo valor na página e cadastre-o manualmente.");

        var inicio = paragrafos.Select(texto => InicioDosEfeitos.Match(texto)).FirstOrDefault(match => match.Success)
            ?? throw new InvalidOperationException("O início dos efeitos da Lei 9.430/1996 não foi encontrado na página do Planalto.");
        var mes = LeituraDePagina.ObterMes(inicio.Groups["mes"].Value)
            ?? throw new InvalidOperationException("O início dos efeitos da Lei 9.430/1996 tem um mês inválido na página do Planalto.");

        return new DescontoMinimoPublicado(
            new DateOnly(int.Parse(inicio.Groups["ano"].Value, CultureInfo.InvariantCulture), mes, 1),
            LeituraDePagina.ExtrairPrimeiroValor(artigo.Valor.Groups["valor"].Value),
            Norma);
    }

    /// <summary>Texto do parágrafo sem os trechos riscados, que são as redações revogadas.</summary>
    private static string TextoEmVigor(HtmlNode paragrafo) => Espacos.Replace(WebUtility.HtmlDecode(string.Concat(paragrafo
        .DescendantsAndSelf()
        .Where(no => no.NodeType == HtmlNodeType.Text && !no.Ancestors().Any(Riscado))
        .Select(no => no.InnerText))), " ").Trim();

    private static bool Riscado(HtmlNode no) => no.Name is "strike" or "s" or "del";
}
