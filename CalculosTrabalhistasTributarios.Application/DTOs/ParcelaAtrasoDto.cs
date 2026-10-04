namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Saldo">Valor devido menos o pago.</param>
/// <param name="FatorCorrecao">Fator acumulado do índice entre o vencimento e o cálculo.</param>
/// <param name="PercentualJuros">Juros acumulados entre o vencimento e o cálculo, em %.</param>
/// <param name="RitoPrisao">Cobrada pelo rito da prisão: uma das 3 parcelas anteriores ao ajuizamento ou vencida no curso do processo.</param>
public sealed record ParcelaAtrasoDto(DateOnly Competencia, DateOnly Vencimento, decimal Devido, decimal Pago, decimal Saldo, decimal FatorCorrecao, decimal Corrigido, decimal PercentualJuros, decimal Juros, decimal Total, bool RitoPrisao);
