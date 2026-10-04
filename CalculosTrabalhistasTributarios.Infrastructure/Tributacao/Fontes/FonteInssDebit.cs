using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Tabelas do INSS do Debit, que costuma publicar a tabela do ano antes da página oficial.</summary>
public sealed partial class FonteInssDebit : IFonteTabela<TabelaInssPublicada>
{
    // Título de cada tabela: "INSS a partir de 01/01/2026".
    [GeneratedRegex(@"a partir de \d{2}/(?<mes>\d{2})/(?<ano>\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex Vigencia { get; }

    public string Nome => "debit.com.br";

    public bool Oficial => false;

    public Uri Endereco { get; } = new("https://www.debit.com.br/tabelas/tabelas-inss");

    public async Task<TabelaInssPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var (competencia, linhas) = await PaginaDebit.ObterTabelaMaisRecenteAsync(httpClient, Endereco, Vigencia, cancellationToken);
        return new TabelaInssPublicada(competencia, LeituraDePagina.LerFaixasInss(linhas));
    }
}
