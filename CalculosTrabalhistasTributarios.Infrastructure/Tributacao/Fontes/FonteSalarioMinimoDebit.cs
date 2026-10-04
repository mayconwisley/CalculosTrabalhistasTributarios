using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Salário mínimo pela 1ª faixa da tabela do INSS publicada pelo Debit (EC 103/2019, art. 28).</summary>
public sealed class FonteSalarioMinimoDebit : IFonteTabela<SalarioMinimoPublicado>
{
    private readonly FonteInssDebit _inss = new();

    public string Nome => "debit.com.br";

    public bool Oficial => false;

    public Uri Endereco => _inss.Endereco;

    public async Task<SalarioMinimoPublicado> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var inss = await _inss.ObterAsync(httpClient, cancellationToken);
        return new SalarioMinimoPublicado(inss.Competencia, inss.Faixas.Count > 0 ? inss.Faixas[0].Limite : 0m);
    }
}
