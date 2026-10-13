namespace CalculosTrabalhistasTributarios.Domain.Previdencia;

/// <summary>A contribuição de uma competência e a complementação que a levaria a 20%.</summary>
/// <param name="LimitadaAoTeto">O salário de contribuição informado passava do teto e foi limitado a ele.</param>
/// <param name="Complementacao">Diferença até 20% do salário mínimo nos planos reduzidos; zero no plano normal.</param>
public sealed record ContribuicaoDoMes(ParametrosContribuicaoMes Parametros, decimal Base, decimal Aliquota, decimal Valor, bool LimitadaAoTeto, decimal Complementacao);
