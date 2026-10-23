using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Tabelas do IRRF do Portal Contábeis. Trazem as faixas, sem o desconto simplificado e a redução mensal.</summary>
public sealed class FonteIrrfContabeis : IFonteTabela<TabelaIrrfPublicada>
{
    public string Nome => "contabeis.com.br";

    public bool Oficial => false;

    public Uri Endereco { get; } = new("https://www.contabeis.com.br/tabelas/imposto-renda/");

    public async Task<TabelaIrrfPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var (competencia, linhas) = await PaginaContabeis.ObterTabelaMaisRecenteAsync(httpClient, Endereco, cancellationToken);
        return new TabelaIrrfPublicada(competencia, LeituraDePagina.LerFaixasIrrf(linhas));
    }
}
