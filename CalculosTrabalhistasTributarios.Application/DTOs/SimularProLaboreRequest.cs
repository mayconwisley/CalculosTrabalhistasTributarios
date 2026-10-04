using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="AliquotaIss">ISS retido do autônomo, quando a lei municipal exige; zero para não reter.</param>
public sealed record SimularProLaboreRequest(
    DateOnly Competencia,
    TipoContribuinteIndividual Tipo,
    decimal Valor,
    int Dependentes,
    decimal AliquotaIss,
    RegimeTributario Regime);
