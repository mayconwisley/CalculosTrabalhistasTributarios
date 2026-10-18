namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.CustoFuncionario;

public sealed record ResultadoCustoFuncionario(
    decimal AliquotaPatronal, decimal AliquotaRat, decimal AliquotaTerceiros, decimal AliquotaFgts,
    decimal AliquotaCompensatoria, decimal Patronal, decimal Rat, decimal Terceiros, decimal Fgts,
    decimal Compensatoria, decimal Provisao13, decimal ProvisaoFerias, decimal EncargosProvisoes,
    decimal Beneficios, decimal Recorrente, decimal Reserva, decimal TotalComReserva, decimal CustoAnual,
    decimal MediaMensal, decimal? Acrescimo, decimal CustoHora)
{
    public decimal SalarioComEncargos => Recorrente - Beneficios;
    public decimal AliquotaEncargos => AliquotaPatronal + AliquotaRat + AliquotaTerceiros + AliquotaFgts + AliquotaCompensatoria;
}
