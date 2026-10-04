using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="ValorBruto">Rendimentos de quem paga a pensão, que também são a base do INSS.</param>
/// <param name="Atual">Pensão em vigor.</param>
/// <param name="Proposta">Pensão pedida ou oferecida na revisão.</param>
public sealed record SimularRevisaoPensaoRequest(DateOnly Competencia, decimal ValorBruto, int Dependentes, RegraPensao Atual, RegraPensao Proposta);
