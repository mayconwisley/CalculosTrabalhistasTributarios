namespace CalculosTrabalhistasTributarios.Domain.Previdencia;

/// <summary>O salário mínimo e o teto do INSS de uma competência.</summary>
public sealed record ParametrosContribuicaoMes(DateOnly Competencia, decimal SalarioMinimo, decimal Teto);
