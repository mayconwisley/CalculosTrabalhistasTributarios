namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// Regras anuais do imposto de renda da pessoa física a partir do ano-calendário 2026 (declaração de 2027), com as
/// mudanças da Lei 15.270/2025: a tabela anual, o desconto simplificado, as deduções, a redução anual (Lei 9.250/1995,
/// art. 11-A) e a tributação mínima das altas rendas (arts. 16-A e 16-B).
/// </summary>
public static class ImpostoRendaAnual
{
    public const int PrimeiroAno = 2026;

    /// <summary>Tabela progressiva anual: a soma das mensais (Lei 11.482/2007, art. 1º, parágrafo único), com as parcelas publicadas pela Receita.</summary>
    public static readonly IReadOnlyList<FaixaTributaria> Tabela =
    [
        new(1, 29_145.60m, 0m, 0m),
        new(2, 33_919.80m, 7.5m, 2_185.92m),
        new(3, 45_012.60m, 15m, 4_729.91m),
        new(4, 55_976.16m, 22.5m, 8_105.85m),
        new(5, decimal.MaxValue, 27.5m, 10_904.66m)
    ];

    public const decimal PercentualSimplificado = 20m;
    public const decimal LimiteSimplificado = 17_640.00m;
    public const decimal DeducaoPorDependente = 2_275.08m;
    public const decimal LimiteInstrucaoPorPessoa = 3_561.50m;
    public const decimal LimitePrevidenciaComplementar = 12m;

    // Redução anual (art. 11-A): até 60 mil de rendimentos tributáveis, zera o imposto até 2.694,15; até 88,2 mil, decresce em linha.
    public const decimal LimiteIsencaoReducao = 60_000.00m;
    public const decimal ReducaoMaxima = 2_694.15m;
    public const decimal LimiteReducao = 88_200.00m;
    public const decimal ReducaoValorBase = 8_429.73m;
    public const decimal ReducaoMultiplicador = 0.095575m;

    // Tributação mínima (art. 16-A): acima de 600 mil de rendimentos no ano, alíquota que cresce até 10% em 1,2 milhão.
    public const decimal LimiteTributacaoMinima = 600_000.00m;
    public const decimal RendaAliquotaMaxima = 1_200_000.00m;
    public const decimal AliquotaMaximaMinima = 10m;

    public static decimal ImpostoPelaTabela(decimal baseCalculo) => CalculadoraTributacao.CalcularPorFaixa(Math.Max(0m, baseCalculo), Tabela);

    public static FaixaTributaria Faixa(decimal baseCalculo) => Tabela.First(faixa => baseCalculo <= faixa.Limite);

    /// <summary>Redução anual sobre os rendimentos tributáveis brutos, nunca maior que o imposto da tabela (art. 11-A, § 1º).</summary>
    public static decimal Reducao(decimal rendimentosTributaveis, decimal impostoTabela)
    {
        var reducao = rendimentosTributaveis <= LimiteIsencaoReducao ? ReducaoMaxima
            : rendimentosTributaveis <= LimiteReducao ? CalculadoraTributacao.Arredondar(ReducaoValorBase - ReducaoMultiplicador * rendimentosTributaveis)
            : 0m;
        return Math.Clamp(reducao, 0m, impostoTabela);
    }

    public static decimal DescontoSimplificado(decimal rendimentosTributaveis) =>
        Math.Min(CalculadoraTributacao.Arredondar(rendimentosTributaveis * PercentualSimplificado / 100m), LimiteSimplificado);

    /// <summary>Alíquota da tributação mínima, em %: zero até 600 mil, (rendimentos ÷ 60.000) − 10 até 1,2 milhão e 10% acima.</summary>
    public static decimal AliquotaMinima(decimal rendimentos) =>
        rendimentos <= LimiteTributacaoMinima ? 0m
        : rendimentos >= RendaAliquotaMaxima ? AliquotaMaximaMinima
        : rendimentos / 60_000m - 10m;
}
