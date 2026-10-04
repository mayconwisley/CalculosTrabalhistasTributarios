namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Totais do mês em minutos, prontos para as calculadoras de horas extras e de holerite.</summary>
/// <param name="ExtrasFaixa1">Horas extras dos dias úteis fora do período noturno.</param>
/// <param name="ExtrasFaixa2">Horas trabalhadas em descansos e feriados.</param>
/// <param name="Noturnas">Horas noturnas que não são extras de dia útil, inclusive as de descansos e feriados, para o adicional noturno.</param>
/// <param name="ExtrasNoturnas">Horas extras noturnas de dias úteis, com o adicional noturno na base (OJ 97 da SDI-1 do TST).</param>
/// <param name="DescansosPerdidos">Semanas com falta injustificada: cada uma perde o descanso remunerado (Lei 605/1949, art. 6º).</param>
/// <param name="Feriados">Feriados em dias de semana, para o DSR.</param>
public sealed record TotaisJornadaDto(
    int Trabalhadas,
    int Previstas,
    int ExtrasFaixa1,
    int ExtrasFaixa2,
    int Noturnas,
    int ExtrasNoturnas,
    int Faltas,
    int DescansosPerdidos,
    int Faltantes,
    int IntervaloSuprimido,
    int InterjornadaSuprimida,
    int Feriados);
