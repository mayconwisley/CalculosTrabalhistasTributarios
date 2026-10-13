namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso;

/// <summary>Horas de sobreaviso e de prontidão de uma competência, com o salário normal do mês.</summary>
/// <param name="Salario">Salário normal do mês, base do salário-hora; sem o adicional de periculosidade (Súmula 132, II, do TST).</param>
/// <param name="HorasSobreaviso">Horas em que o empregado aguardou o chamado fora da empresa, sem as horas em que trabalhou.</param>
/// <param name="HorasProntidao">Horas em que o empregado aguardou ordens nas dependências da empresa.</param>
/// <param name="Feriados">Feriados em dias úteis do mês, para o DSR.</param>
public sealed record MesSobreaviso(DateOnly Competencia, decimal Salario, decimal HorasSobreaviso, decimal HorasProntidao, int Feriados = 0);
