using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Periodos">Entradas e saídas na ordem do dia; uma saída menor que a entrada passa da meia-noite.</param>
/// <param name="MinutosPrevistos">Jornada contratual do dia; nos dias de descanso e feriados, zero.</param>
public sealed record MarcacaoDia(DateOnly Data, TipoDia Tipo, int MinutosPrevistos, IReadOnlyList<PeriodoTrabalhado> Periodos);
