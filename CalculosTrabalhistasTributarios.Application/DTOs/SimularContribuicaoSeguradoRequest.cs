using CalculosTrabalhistasTributarios.Domain.Previdencia;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="SalarioContribuicao">Base do plano normal; nos planos reduzidos, a base é o salário mínimo da competência.</param>
public sealed record SimularContribuicaoSeguradoRequest(
    DateOnly Competencia,
    CategoriaSegurado Categoria,
    PlanoContribuicao Plano,
    PeriodicidadeContribuicao Periodicidade,
    decimal SalarioContribuicao);
