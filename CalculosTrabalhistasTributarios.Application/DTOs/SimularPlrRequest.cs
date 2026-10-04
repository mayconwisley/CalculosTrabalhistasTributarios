using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="PlrAnterior">PLR já paga no mesmo ano; o imposto é recalculado sobre o total do ano (Lei 10.101/2000, art. 3º, § 7º).</param>
/// <param name="ImpostoRetidoAnterior">IRRF já retido sobre a PLR anterior, descontado do imposto recalculado.</param>
/// <param name="Pensao">Pensão alimentícia judicial descontada desta PLR, que reduz a base do imposto; nula quando não há.</param>
public sealed record SimularPlrRequest(
    DateOnly Competencia,
    decimal Valor,
    decimal PlrAnterior,
    decimal ImpostoRetidoAnterior,
    RegraPensao? Pensao = null);
