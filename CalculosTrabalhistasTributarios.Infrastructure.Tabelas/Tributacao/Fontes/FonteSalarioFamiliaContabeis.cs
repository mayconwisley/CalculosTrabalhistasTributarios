using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Salário-família do Portal Contábeis, no mesmo formato de seções das demais tabelas do site.</summary>
public sealed class FonteSalarioFamiliaContabeis : IFonteTabela<TabelaSalarioFamiliaPublicada>
{
    public string Nome => "contabeis.com.br";

    public bool Oficial => false;

    public Uri Endereco { get; } = new("https://www.contabeis.com.br/tabelas/salario-familia/");

    public async Task<TabelaSalarioFamiliaPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var (competencia, linhas) = await PaginaContabeis.ObterTabelaMaisRecenteAsync(httpClient, Endereco, cancellationToken);
        return new TabelaSalarioFamiliaPublicada(competencia, FonteSalarioFamiliaDebit.LerFaixas(linhas));
    }
}
