namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <param name="Competencia">Primeiro dia do mês da verba.</param>
/// <param name="Valor">Valor da verba no mês, sem o DSR.</param>
/// <param name="Feriados">Feriados em dias úteis do mês, que aumentam os dias de descanso do DSR.</param>
public sealed record MesVerbaReflexa(DateOnly Competencia, decimal Valor, int Feriados = 0);
