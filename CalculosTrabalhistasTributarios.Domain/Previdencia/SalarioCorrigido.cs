namespace CalculosTrabalhistasTributarios.Domain.Previdencia;

/// <summary>O salário de contribuição de uma competência e o valor dele corrigido pelo INPC.</summary>
/// <param name="Fator">Produto de (1 + INPC do mês) da competência até o último mês da correção; 1 quando não há correção.</param>
public sealed record SalarioCorrigido(DateOnly Competencia, decimal Salario, decimal Fator, decimal Corrigido);
