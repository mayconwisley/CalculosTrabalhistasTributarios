using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Salário-família do Debit: tabelas com a vigência no título e uma linha por faixa (limite e cota).</summary>
public sealed partial class FonteSalarioFamiliaDebit : IFonteTabela<TabelaSalarioFamiliaPublicada>
{
    // Título de cada tabela: "INSS a partir de 01/01/2026".
    [GeneratedRegex(@"a partir de \d{2}/(?<mes>\d{2})/(?<ano>\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex Vigencia { get; }

    public string Nome => "debit.com.br";

    public bool Oficial => false;

    public Uri Endereco { get; } = new("https://www.debit.com.br/tabelas/salario-familia");

    public async Task<TabelaSalarioFamiliaPublicada> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var (competencia, linhas) = await PaginaDebit.ObterTabelaMaisRecenteAsync(httpClient, Endereco, Vigencia, cancellationToken);
        return new TabelaSalarioFamiliaPublicada(competencia, LerFaixas(linhas));
    }

    /// <summary>Linhas "limite de remuneração | cota"; a linha "acima de", sem cota, fica de fora.</summary>
    internal static FaixaSalarioFamiliaPublicada[] LerFaixas(IEnumerable<string[]> linhas) => linhas
        .Where(colunas => colunas.Length >= 2 && LeituraDePagina.TemValorMonetario(colunas[0]) && LeituraDePagina.TemValorMonetario(colunas[1]))
        .Select(colunas => new FaixaSalarioFamiliaPublicada(LeituraDePagina.ExtrairUltimoValor(colunas[0]), LeituraDePagina.ExtrairUltimoValor(colunas[1])))
        .ToArray();
}
