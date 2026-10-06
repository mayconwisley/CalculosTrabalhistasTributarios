namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Saldo">Soma dos saldos de todas as contas do FGTS.</param>
/// <param name="MesAniversario">Mês de nascimento, de 1 a 12, que abre o período de saque.</param>
public sealed record SimularSaqueAniversarioRequest(decimal Saldo, int MesAniversario, decimal ParcelaComprometida = 0m);
