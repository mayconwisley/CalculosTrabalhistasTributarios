namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>
/// Indenização compensatória do doméstico: 3,2% depositados todo mês, que substituem a multa de 40% do FGTS
/// (LC 150/2015, art. 22). Na dispensa sem justa causa, o empregado saca tudo; no acordo, a metade; nos demais motivos,
/// o valor volta ao empregador.
/// </summary>
/// <param name="Deposito">3,2% das verbas do mês, do aviso indenizado e do 13º.</param>
/// <param name="Saldo">Saldo antes do mês do desligamento, estimado em 40% do saldo do FGTS, porque 3,2% é 40% de 8%.</param>
/// <param name="PercentualAoEmpregado">100 na dispensa sem justa causa, 50 no acordo e zero nos demais motivos.</param>
public sealed record CompensatoriaDomestico(decimal Deposito, decimal Saldo, decimal PercentualAoEmpregado, decimal AoEmpregado)
{
    public decimal AoEmpregador => Saldo + Deposito - AoEmpregado;
}
