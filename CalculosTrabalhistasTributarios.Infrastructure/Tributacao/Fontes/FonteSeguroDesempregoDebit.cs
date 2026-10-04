using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Seguro-desemprego do Debit: o histórico das tabelas, cada uma com a referência no título.</summary>
public sealed partial class FonteSeguroDesempregoDebit : IFonteTabela<TabelaSeguroDesempregoPublicada>
{
    // Título de cada tabela: "Seguro desemprego ref. 01/2026".
    [GeneratedRegex(@"ref\.?\s*(?<mes>\d{2})/(?<ano>\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex Vigencia { get; }

    public string Nome => "debit.com.br";

    public bool Oficial => false;

    public Uri Endereco { get; } = new("https://www.debit.com.br/tabelas/seguro-desemprego");

    public async Task<TabelaSeguroDesempregoPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var (competencia, linhas) = await PaginaDebit.ObterTabelaMaisRecenteAsync(httpClient, Endereco, Vigencia, cancellationToken);
        return new TabelaSeguroDesempregoPublicada(competencia, LeituraDePagina.LerFaixasSeguroDesemprego(linhas));
    }
}
