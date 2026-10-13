namespace CalculosTrabalhistasTributarios.Domain.Previdencia;

/// <summary>A contribuição a simular e os parâmetros de cada competência do período, na ordem.</summary>
/// <param name="SalarioContribuicao">Base escolhida no plano normal, entre o salário mínimo e o teto; ignorada nos demais.</param>
/// <param name="Meses">A competência, no recolhimento mensal, ou os três meses do trimestre civil.</param>
public sealed record EntradaContribuicaoSegurado(
    CategoriaSegurado Categoria,
    PlanoContribuicao Plano,
    PeriodicidadeContribuicao Periodicidade,
    decimal SalarioContribuicao,
    IReadOnlyList<ParametrosContribuicaoMes> Meses);
