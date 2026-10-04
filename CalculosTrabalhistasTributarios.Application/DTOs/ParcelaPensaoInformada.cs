namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Devido">Valor da parcela no vencimento.</param>
/// <param name="Pago">Pagamento parcial, abatido do valor da parcela no vencimento.</param>
public sealed record ParcelaPensaoInformada(DateOnly Competencia, DateOnly Vencimento, decimal Devido, decimal Pago);
