using HtmlAgilityPack;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>Leitura das páginas de tabelas, comum a todas as fontes.</summary>
internal static partial class LeituraDePagina
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Meses = Cultura.DateTimeFormat.MonthNames.Take(12).Select(mes => Normalizar(mes).ToLowerInvariant()).ToArray();

    [GeneratedRegex(@"\d{1,3}(?:\.\d{3})*,\d{2}")]
    private static partial Regex ValorMonetario { get; }

    // Aceita vírgula ou ponto decimal: há fonte que publica "7.5%".
    [GeneratedRegex(@"\d{1,2}(?:[,.]\d+)?")]
    private static partial Regex Percentual { get; }

    // Fator da parcela do seguro-desemprego: "multiplica-se por 0,8" ou, quando não há fator, "(80%)".
    [GeneratedRegex(@"por\s+(?<fator>0,\d{1,4})|(?<percentual>\d{1,3}(?:,\d+)?)\s*%", RegexOptions.IgnoreCase)]
    private static partial Regex FatorDaParcela { get; }

    private static readonly UTF8Encoding Utf8Estrito = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static async Task<HtmlDocument> ObterDocumentoAsync(HttpClient httpClient, Uri endereco, CancellationToken cancellationToken)
    {
        using var resposta = await httpClient.GetAsync(endereco, cancellationToken);
        resposta.EnsureSuccessStatusCode();

        var documento = new HtmlDocument();
        documento.LoadHtml(await LerTextoAsync(resposta.Content, cancellationToken));
        return documento;
    }

    /// <summary>
    /// Sem o charset no cabeçalho, o .NET supõe UTF-8 e troca os acentos de páginas antigas, como as leis do Planalto,
    /// que vêm em Windows-1252; nesse caso, o que não for UTF-8 válido é lido como Latin-1.
    /// </summary>
    private static async Task<string> LerTextoAsync(HttpContent conteudo, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(conteudo.Headers.ContentType?.CharSet))
            return await conteudo.ReadAsStringAsync(cancellationToken);

        var bytes = await conteudo.ReadAsByteArrayAsync(cancellationToken);
        try
        {
            return Utf8Estrito.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    /// <summary>Texto das células de cada linha com dados (as linhas de cabeçalho, só com &lt;th&gt;, ficam de fora).</summary>
    public static string[][] LerLinhas(HtmlNode tabela) =>
        (tabela.SelectNodes(".//tr[td]") ?? Enumerable.Empty<HtmlNode>())
            .Select(linha => LerTextos(linha.SelectNodes("./td")))
            .ToArray();

    public static string[] LerTextos(HtmlNodeCollection? nos) =>
        (nos ?? Enumerable.Empty<HtmlNode>()).Select(no => WebUtility.HtmlDecode(no.InnerText).Trim()).ToArray();

    /// <summary>Faixas de INSS a partir de linhas "salário de contribuição | alíquota".</summary>
    public static FaixaInssPublicada[] LerFaixasInss(IEnumerable<string[]> linhas) => linhas
        .Where(colunas => colunas.Length >= 2 && ValorMonetario.IsMatch(colunas[0]))
        .Select(colunas => new FaixaInssPublicada(ExtrairUltimoValor(colunas[0]), ExtrairPercentual(colunas[1])))
        .ToArray();

    /// <summary>Faixas do IRRF a partir de linhas "base de cálculo | alíquota | dedução"; a última faixa recebe o limite que representa "acima de".</summary>
    public static FaixaIrrfPublicada[] LerFaixasIrrf(IEnumerable<string[]> linhas)
    {
        var faixas = linhas
            .Where(colunas => colunas.Length >= 3 && ValorMonetario.IsMatch(colunas[0]))
            .Select(colunas => new FaixaIrrfPublicada(ExtrairUltimoValor(colunas[0]), ExtrairPercentual(colunas[1]), ExtrairUltimoValor(colunas[2])))
            .ToArray();
        if (faixas.Length > 0)
            faixas[^1] = faixas[^1] with { Limite = TabelaIrrfPublicada.LimiteUltimaFaixa };
        return faixas;
    }

    /// <summary>
    /// Faixas do seguro-desemprego a partir das linhas "faixa de salário médio | cálculo da parcela": 80% da média até a 1ª faixa,
    /// o valor fixo mais 50% do excedente até a 2ª e, acima dela, o valor máximo, gravado como o valor fixo de uma faixa "acima de".
    /// </summary>
    public static FaixaSeguroDesempregoPublicada[] LerFaixasSeguroDesemprego(IEnumerable<string[]> linhas)
    {
        // As linhas de cabeçalho não têm valores; há fonte que escreve "Acima de R$ 3703.99", sem o formato brasileiro, e por isso
        // o limite de cada faixa vem da coluna da faixa e o valor da parcela, da coluna do cálculo.
        var faixas = linhas.Where(colunas => colunas.Length >= 2 && (TemValorMonetario(colunas[0]) || TemValorMonetario(colunas[1]))).ToArray();
        if (faixas.Length != 3)
            return [];

        return
        [
            new(ExtrairUltimoValor(faixas[0][0]), ExtrairFator(faixas[0][1]), 0m),
            new(ExtrairUltimoValor(faixas[1][0]), ExtrairFator(faixas[1][1]), ExtrairUltimoValor(faixas[1][1])),
            new(TabelaIrrfPublicada.LimiteUltimaFaixa, 0m, ExtrairUltimoValor(faixas[2][1]))
        ];
    }

    /// <summary>Percentual da média ou do excedente: "por 0,8" resulta em 80; sem fator na célula, zero.</summary>
    private static decimal ExtrairFator(string texto)
    {
        var match = FatorDaParcela.Match(texto);
        if (!match.Success) return 0m;
        return match.Groups["fator"].Success
            ? decimal.Parse(match.Groups["fator"].Value, Cultura) * 100m
            : decimal.Parse(match.Groups["percentual"].Value, Cultura);
    }

    public static bool TemValorMonetario(string texto) => ValorMonetario.IsMatch(texto);

    public static decimal ExtrairUltimoValor(string texto) =>
        ValorMonetario.Matches(texto).Select(match => decimal.Parse(match.Value, Cultura)).LastOrDefault();

    public static decimal ExtrairPrimeiroValor(string texto)
    {
        var match = ValorMonetario.Match(texto);
        return match.Success ? decimal.Parse(match.Value, Cultura) : 0m;
    }

    /// <summary>Alíquota da célula; "isento" ou "-" resultam em zero.</summary>
    public static decimal ExtrairPercentual(string texto)
    {
        var match = Percentual.Match(texto);
        return match.Success ? decimal.Parse(match.Value.Replace(',', '.'), CultureInfo.InvariantCulture) : 0m;
    }

    /// <summary>Número do mês por extenso, como "Março" ou "marco"; nulo se o texto não for um mês.</summary>
    public static int? ObterMes(string nome)
    {
        var indice = Array.IndexOf(Meses, Normalizar(nome).ToLowerInvariant());
        return indice >= 0 ? indice + 1 : null;
    }

    public static string Normalizar(string valor)
    {
        var semAcentos = string.Concat(WebUtility.HtmlDecode(valor)
            .Normalize(NormalizationForm.FormD)
            .Where(caractere => CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark));
        return semAcentos.Replace(' ', ' ').Trim();
    }
}
