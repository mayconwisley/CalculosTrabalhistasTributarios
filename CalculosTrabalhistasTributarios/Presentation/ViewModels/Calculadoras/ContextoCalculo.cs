namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Competência, salário e dependentes de um cálculo, aproveitados para preencher a próxima calculadora aberta.</summary>
public sealed record ContextoCalculo(DateOnly? Competencia, decimal? Salario, int? Dependentes);
