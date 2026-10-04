using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>
/// Salário mínimo pela tabela oficial de contribuição do INSS: desde a EC 103/2019 (art. 28), a 1ª faixa vai até
/// exatamente um salário mínimo.
/// </summary>
public sealed class FonteSalarioMinimoGovBr : IFonteTabela<SalarioMinimoPublicado>
{
    private readonly FonteInssGovBr _inss = new();

    public string Nome => "gov.br (INSS)";

    public bool Oficial => true;

    public Uri Endereco => _inss.Endereco;

    public async Task<SalarioMinimoPublicado> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var inss = await _inss.ObterAsync(httpClient, cancellationToken);
        return new SalarioMinimoPublicado(inss.Competencia, inss.Faixas.Count > 0 ? inss.Faixas[0].Limite : 0m);
    }
}
