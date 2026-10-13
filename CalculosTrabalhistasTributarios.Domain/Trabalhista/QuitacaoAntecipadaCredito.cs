namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>Quitação do crédito depois de algumas parcelas pagas: o saldo pela taxa do contrato e os juros futuros abatidos.</summary>
/// <param name="ParcelasPagas">Parcelas já pagas antes da quitação.</param>
/// <param name="ValorDasRestantes">Soma nominal das parcelas que faltavam, como seriam pagas no prazo.</param>
/// <param name="SaldoParaQuitar">Valor presente das parcelas restantes pela taxa do contrato: o saldo devedor da tabela Price.</param>
public sealed record QuitacaoAntecipadaCredito(int ParcelasPagas, int ParcelasRestantes, decimal ValorDasRestantes, decimal SaldoParaQuitar)
{
    /// <summary>Juros futuros que deixam de ser pagos com a quitação.</summary>
    public decimal JurosAbatidos => ValorDasRestantes - SaldoParaQuitar;
}
