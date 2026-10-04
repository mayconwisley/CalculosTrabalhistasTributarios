namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>O FGTS da rescisão: o depósito do mês, o saldo, a multa e o saque.</summary>
/// <param name="Deposito">8% das verbas do mês, do aviso indenizado e do 13º, menos a 1ª parcela do 13º, que já teve FGTS.</param>
/// <param name="UsaSaldo">O motivo tem multa ou saque, e por isso o saldo da conta importa.</param>
/// <param name="MesesDepositados">Meses de contrato antes do mês do desligamento, usados na estimativa do saldo.</param>
/// <param name="MesesAnosAnteriores">Meses dos anos anteriores, que geraram o FGTS do 13º.</param>
/// <param name="SaldoEstimado">O saldo não foi informado e foi estimado pelo salário atual.</param>
/// <param name="PercentualDeposito">8%, ou 2% do jovem aprendiz.</param>
/// <param name="Compensatoria">Só do doméstico, que não tem a multa de 40%.</param>
public sealed record FgtsRescisao(
    decimal Deposito,
    decimal PercentualMulta,
    decimal PercentualSaque,
    bool UsaSaldo,
    int MesesDepositados,
    int MesesAnosAnteriores,
    bool SaldoEstimado,
    decimal Saldo,
    decimal Multa,
    decimal Saque,
    decimal PercentualDeposito = 8m,
    CompensatoriaDomestico? Compensatoria = null);
