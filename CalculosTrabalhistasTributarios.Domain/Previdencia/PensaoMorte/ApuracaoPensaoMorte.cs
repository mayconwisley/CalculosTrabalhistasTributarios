namespace CalculosTrabalhistasTributarios.Domain.Previdencia.PensaoMorte;

/// <summary>A pensão, a cota de cada dependente, a duração para o cônjuge e o efeito do redutor da acumulação.</summary>
/// <param name="Base">Aposentadoria recebida ou a aposentadoria por incapacidade permanente a que o segurado teria direito.</param>
/// <param name="CoeficienteBase">Percentual da média que formou a base; nulo quando o segurado era aposentado.</param>
/// <param name="Cota">Cota familiar: 50% mais 10 pontos por dependente, até 100%; 100% com dependente inválido.</param>
/// <param name="Calculada">Base × cota, antes da elevação ao salário mínimo.</param>
/// <param name="DuracaoMeses">Duração da cota do cônjuge em meses; nula quando vitalícia ou sem cônjuge.</param>
/// <param name="Vitalicia">A cota do cônjuge é vitalícia.</param>
/// <param name="ReduzidoPensao">Com acumulação, verdadeiro se o benefício reduzido é a cota da pensão; falso se é o benefício próprio.</param>
/// <param name="ValorReduzido">O benefício menos vantajoso depois do redutor; nulo sem acumulação.</param>
public sealed record ApuracaoPensaoMorte(
    EntradaPensaoMorte Entrada,
    decimal Base,
    decimal? CoeficienteBase,
    int Dependentes,
    decimal Cota,
    decimal Calculada,
    decimal Pensao,
    decimal CotaPorDependente,
    int? DuracaoMeses,
    bool Vitalicia,
    bool ReduzidoPensao,
    decimal? ValorReduzido)
{
    /// <summary>A pensão calculada ficava abaixo do salário mínimo e foi elevada a ele.</summary>
    public bool ElevadaAoMinimo => Pensao > Calculada;

    public DateOnly? FimConjuge => DuracaoMeses is { } meses ? Entrada.DataObito.AddMonths(meses) : null;

    /// <summary>O que o cônjuge recebe com a acumulação: o benefício mais vantajoso inteiro e o outro reduzido.</summary>
    public decimal? TotalConjuge => ValorReduzido is { } reduzido ? Math.Max(CotaPorDependente, Entrada.BeneficioConjuge) + reduzido : null;
}
