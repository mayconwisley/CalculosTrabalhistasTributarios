using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="MinutosExtras">Além da jornada do dia útil, ou todo o trabalho em descanso e feriado.</param>
/// <param name="MinutosNoturnos">Minutos de relógio no período noturno, inclusive na prorrogação depois dele (Súmula 60 do TST).</param>
/// <param name="MinutosNoturnosExtras">Parte noturna das horas extras.</param>
/// <param name="MinutosFaltantes">Atrasos e saídas antecipadas além da tolerância.</param>
/// <param name="IntervaloSuprimido">Parte do intervalo de descanso e alimentação que não foi concedida (CLT, art. 71).</param>
/// <param name="InterjornadaSuprimida">Parte das 11 horas entre duas jornadas que não foi respeitada (CLT, art. 66).</param>
public sealed record DiaJornadaDto(
    DateOnly Data,
    TipoDia Tipo,
    int MinutosPrevistos,
    int MinutosTrabalhados,
    int MinutosExtras,
    int MinutosNoturnos,
    int MinutosNoturnosExtras,
    int MinutosFaltantes,
    bool Falta,
    int IntervaloSuprimido,
    int InterjornadaSuprimida)
{
    public bool Trabalhado => MinutosTrabalhados > 0;
    public bool ExtraComAdicionalDeDescanso => Tipo != TipoDia.Util;
}
