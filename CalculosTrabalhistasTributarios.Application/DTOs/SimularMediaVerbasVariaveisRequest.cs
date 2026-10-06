using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record SimularMediaVerbasVariaveisRequest(IReadOnlyList<VerbasVariaveisDoMes> Meses, int Divisor);
