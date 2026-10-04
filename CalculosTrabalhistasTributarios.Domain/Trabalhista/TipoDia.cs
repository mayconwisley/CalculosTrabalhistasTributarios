namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>
/// Útil: dia de trabalho, com a jornada prevista (que pode ser zero, como no sábado compensado). Descanso e feriado: o
/// trabalho nesses dias é todo hora extra com o adicional de domingos e feriados.
/// </summary>
public enum TipoDia { Util, Descanso, Feriado }
