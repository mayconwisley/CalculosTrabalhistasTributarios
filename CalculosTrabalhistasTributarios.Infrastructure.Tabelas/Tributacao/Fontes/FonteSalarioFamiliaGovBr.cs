using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using HtmlAgilityPack;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Página oficial do INSS com o limite de remuneração e a cota do salário-família de cada período.</summary>
public sealed partial class FonteSalarioFamiliaGovBr : IFonteTabela<TabelaSalarioFamiliaPublicada>
{
    // "A partir de 01/01/2026" ou "A partir de 1º/01/2022".
    [GeneratedRegex(@"a partir de (?<dia>\d{1,2})º?/(?<mes>\d{1,2})/(?<ano>\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex Vigencia { get; }

    // "Até 1.980,38 cota 67,54" ou "de 907,77 a 1.364,43 cota 32,80".
    [GeneratedRegex(@"(?<limite>\d{1,3}(?:\.\d{3})*,\d{2})\s*cota\s*(?<cota>\d{1,3}(?:\.\d{3})*,\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex FaixaComCota { get; }

    public string Nome => "gov.br (INSS)";

    public bool Oficial => true;

    public Uri Endereco { get; } = new("https://www.gov.br/inss/pt-br/direitos-e-deveres/salario-familia/valor-limite-para-direito-ao-salario-familia");

    public async Task<TabelaSalarioFamiliaPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var documento = await LeituraDePagina.ObterDocumentoAsync(httpClient, Endereco, cancellationToken);
        // Uma linha por período: a vigência, uma coluna por faixa e o normativo; vale a mais recente.
        var maisRecente = (documento.DocumentNode.SelectNodes("//table") ?? Enumerable.Empty<HtmlNode>())
            .SelectMany(LeituraDePagina.LerLinhas)
            .Select(colunas => (Colunas: colunas, Vigencia: colunas.Length > 0 ? Vigencia.Match(LeituraDePagina.Normalizar(colunas[0])) : Match.Empty))
            .Where(item => item.Vigencia.Success)
            .Select(item => (Competencia: new DateOnly(int.Parse(item.Vigencia.Groups["ano"].Value, CultureInfo.InvariantCulture), int.Parse(item.Vigencia.Groups["mes"].Value, CultureInfo.InvariantCulture), 1), item.Colunas))
            .OrderByDescending(item => item.Competencia)
            .FirstOrDefault();

        if (maisRecente.Colunas is null)
            throw new InvalidOperationException("A tabela do salário-família não foi encontrada na página do INSS.");

        var faixas = maisRecente.Colunas.Skip(1)
            .Select(coluna => FaixaComCota.Match(coluna))
            .Where(match => match.Success)
            .Select(match => new FaixaSalarioFamiliaPublicada(LeituraDePagina.ExtrairUltimoValor(match.Groups["limite"].Value), LeituraDePagina.ExtrairUltimoValor(match.Groups["cota"].Value)))
            .ToArray();
        return new TabelaSalarioFamiliaPublicada(maisRecente.Competencia, faixas);
    }
}
