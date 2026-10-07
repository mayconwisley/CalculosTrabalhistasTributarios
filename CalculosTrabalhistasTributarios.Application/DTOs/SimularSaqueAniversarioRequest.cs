using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Saldo">Saldo copiado do extrato, livre ou total conforme SaldoIncluiGarantia; pode conter a multa indicada separadamente.</param>
/// <param name="MesAniversario">Mês de nascimento, de 1 a 12, que abre o período de saque.</param>
/// <param name="GarantiaBloqueada">Saldo bloqueado somado à base somente quando não estiver incluído em Saldo.</param>
/// <param name="MultaRescisoriaIncluida">Multa contida em Saldo, excluída da base.</param>
public sealed record SimularSaqueAniversarioRequest(decimal Saldo, int MesAniversario, decimal ParcelaComprometida = 0m,
    decimal GarantiaBloqueada = 0m, decimal MultaRescisoriaIncluida = 0m, bool SaldoIncluiGarantia = false,
    EntradaAnaliseAntecipacaoFgts? AnaliseAntecipacao = null);
