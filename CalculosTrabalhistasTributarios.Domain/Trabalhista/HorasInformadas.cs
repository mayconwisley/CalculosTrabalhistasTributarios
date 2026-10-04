namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <param name="HorasNoturnas">Horas normais de relógio entre 22h e 5h.</param>
/// <param name="HorasExtrasNoturnas">Horas extras de relógio no período noturno, com o adicional da faixa 1.</param>
/// <param name="Rural">Trabalho rural: a hora noturna é a de relógio, sem a redução para 52 minutos e 30 segundos.</param>
public sealed record HorasInformadas(
    decimal HorasFaixa1,
    decimal PercentualFaixa1,
    decimal HorasFaixa2,
    decimal PercentualFaixa2,
    decimal HorasNoturnas,
    decimal PercentualNoturno,
    decimal HorasExtrasNoturnas,
    int Feriados,
    bool Rural = false);
