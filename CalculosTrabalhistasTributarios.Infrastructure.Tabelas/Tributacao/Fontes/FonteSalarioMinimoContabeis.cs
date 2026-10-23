using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Globalization;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Salário mínimo do Portal Contábeis: cada linha traz o ano, a data de vigência, o valor e o ato legal.</summary>
public sealed class FonteSalarioMinimoContabeis : IFonteTabela<SalarioMinimoPublicado>
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public string Nome => "contabeis.com.br";

    public bool Oficial => false;

    public Uri Endereco { get; } = new("https://www.contabeis.com.br/tabelas/salario-minimo/");

    public async Task<SalarioMinimoPublicado> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var (_, linhas) = await PaginaContabeis.ObterTabelaMaisRecenteAsync(httpClient, Endereco, cancellationToken);
        // A data de vigência da linha é mais precisa que o título da seção, por exemplo nos reajustes de maio.
        var maisRecente = linhas
            .Where(colunas => colunas.Length >= 3 && LeituraDePagina.TemValorMonetario(colunas[2]))
            .Select(colunas => (Valido: DateOnly.TryParseExact(colunas[1].Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var vigencia), Vigencia: vigencia, Valor: LeituraDePagina.ExtrairUltimoValor(colunas[2])))
            .Where(item => item.Valido)
            .OrderByDescending(item => item.Vigencia)
            .FirstOrDefault();

        if (!maisRecente.Valido)
            throw new InvalidOperationException("O salário mínimo não foi encontrado na página.");
        return new SalarioMinimoPublicado(new DateOnly(maisRecente.Vigencia.Year, maisRecente.Vigencia.Month, 1), maisRecente.Valor);
    }
}
