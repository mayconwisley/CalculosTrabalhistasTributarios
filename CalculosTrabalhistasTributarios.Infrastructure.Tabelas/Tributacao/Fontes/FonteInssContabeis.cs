using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Tabelas do INSS do Portal Contábeis, que costuma publicar a tabela do ano antes da página oficial.</summary>
public sealed class FonteInssContabeis : IFonteTabela<TabelaInssPublicada>
{
    public string Nome => "contabeis.com.br";

    public bool Oficial => false;

    public Uri Endereco { get; } = new("https://www.contabeis.com.br/tabelas/inss/");

    public async Task<TabelaInssPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var (competencia, linhas) = await PaginaContabeis.ObterTabelaMaisRecenteAsync(httpClient, Endereco, cancellationToken);
        return new TabelaInssPublicada(competencia, LeituraDePagina.LerFaixasInss(linhas));
    }
}
