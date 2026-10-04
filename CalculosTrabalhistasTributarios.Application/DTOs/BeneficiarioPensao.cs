
using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Uma pensão da decisão: quem recebe e como o valor é definido.</summary>
public sealed record BeneficiarioPensao(string Nome, RegraPensao Regra);
