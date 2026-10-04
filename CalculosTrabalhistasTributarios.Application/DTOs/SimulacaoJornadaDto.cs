using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record SimulacaoJornadaDto(
    DateOnly Competencia,
    TrabalhoNoturno Noturno,
    IReadOnlyList<DiaJornadaDto> Dias,
    TotaisJornadaDto Totais,
    IReadOnlyList<string> Criterios,
    IReadOnlyList<string> Observacoes);
