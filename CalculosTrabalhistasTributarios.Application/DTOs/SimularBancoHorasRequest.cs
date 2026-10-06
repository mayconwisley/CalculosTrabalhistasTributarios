using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record SimularBancoHorasRequest(DateOnly Inicio, DateOnly Fim, RegimeBancoHoras Regime,
    SituacaoBancoHoras Situacao, decimal Salario, decimal Divisor, decimal Adicional,
    IReadOnlyList<LancamentoBancoHoras> Lancamentos);
