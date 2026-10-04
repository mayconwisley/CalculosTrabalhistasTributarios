using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record ApurarJornadaRequest(DateOnly Competencia, TrabalhoNoturno Noturno, IReadOnlyList<MarcacaoDia> Dias);
