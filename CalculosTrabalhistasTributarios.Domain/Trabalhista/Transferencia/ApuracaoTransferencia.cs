using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Transferencia;

/// <summary>O adicional de cada mês da transferência e os seus reflexos, apurados pelo motor de reflexos.</summary>
public sealed record ApuracaoTransferencia(EntradaTransferencia Entrada, IReadOnlyList<AdicionalTransferenciaDoMes> Meses, ApuracaoReflexos Reflexos)
{
    /// <summary>O adicional e os reflexos, mais o reembolso das despesas da mudança.</summary>
    public decimal TotalAoEmpregado => Reflexos.TotalAoEmpregado + Entrada.Despesas;
}
