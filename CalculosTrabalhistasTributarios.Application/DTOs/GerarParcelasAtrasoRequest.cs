using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Monta as parcelas de um período: um valor fixo por mês ou um percentual do salário mínimo de cada mês.</summary>
/// <param name="Base"><see cref="BasePensao.ValorFixo"/> ou <see cref="BasePensao.SalarioMinimo"/>.</param>
public sealed record GerarParcelasAtrasoRequest(BasePensao Base, decimal Valor, decimal Percentual, DateOnly PrimeiraParcela, DateOnly UltimaParcela, int DiaVencimento);
