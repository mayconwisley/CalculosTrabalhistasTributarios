namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="ValorHora">Valor da hora de trabalho, não menor que o do salário mínimo nem que o dos empregados da mesma função.</param>
/// <param name="Horas">Horas trabalhadas no período da convocação.</param>
/// <param name="DiasTrabalhados">Dias trabalhados no período.</param>
/// <param name="Descansos">Domingos e feriados dentro do período, que geram o DSR.</param>
public sealed record SimularIntermitenteRequest(DateOnly Competencia, decimal ValorHora, decimal Horas, int DiasTrabalhados, int Descansos);
