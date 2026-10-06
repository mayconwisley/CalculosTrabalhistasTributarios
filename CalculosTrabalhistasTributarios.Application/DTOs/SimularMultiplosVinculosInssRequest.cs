using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record SimularMultiplosVinculosInssRequest(DateOnly Competencia, IReadOnlyList<VinculoInss> Vinculos);
