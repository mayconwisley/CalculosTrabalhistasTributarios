namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="HorasNoturnas">Horas normais de relógio trabalhadas entre 22h e 5h, convertidas para a hora noturna reduzida.</param>
/// <param name="AdicionaisSalariais">Adicionais mensais de natureza salarial, como insalubridade e periculosidade, que integram o valor da hora.</param>
/// <param name="HorasExtrasNoturnas">Horas extras de relógio feitas no período noturno, com o adicional da faixa 1 sobre a hora noturna.</param>
/// <param name="Rural">Trabalho rural: noite das 21h às 5h na lavoura e das 20h às 4h na pecuária, sem hora reduzida (Lei 5.889/1973, art. 7º).</param>
public sealed record SimularHorasExtrasRequest(
    DateOnly Competencia,
    decimal Salario,
    decimal Divisor,
    decimal HorasFaixa1,
    decimal PercentualFaixa1,
    decimal HorasFaixa2,
    decimal PercentualFaixa2,
    decimal HorasNoturnas,
    decimal PercentualNoturno,
    int Feriados,
    int Dependentes,
    decimal AdicionaisSalariais = 0m,
    decimal HorasExtrasNoturnas = 0m,
    bool Rural = false);
