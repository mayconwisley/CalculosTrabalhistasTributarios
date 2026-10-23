using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Tabelas do IRRF do Debit. Trazem só as faixas, sem o desconto simplificado e a redução mensal.</summary>
public sealed partial class FonteIrrfDebit : IFonteTabela<TabelaIrrfPublicada>
{
    // Título de cada tabela: "Tabela de IRRF de 05/2025 a 10/2026".
    [GeneratedRegex(@"de (?<mes>\d{2})/(?<ano>\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex Vigencia { get; }

    public string Nome => "debit.com.br";

    public bool Oficial => false;

    public Uri Endereco { get; } = new("https://www.debit.com.br/tabelas/tabelas-irrf");

    public async Task<TabelaIrrfPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var (competencia, linhas) = await PaginaDebit.ObterTabelaMaisRecenteAsync(httpClient, Endereco, Vigencia, cancellationToken);
        return new TabelaIrrfPublicada(competencia, LeituraDePagina.LerFaixasIrrf(linhas));
    }
}
