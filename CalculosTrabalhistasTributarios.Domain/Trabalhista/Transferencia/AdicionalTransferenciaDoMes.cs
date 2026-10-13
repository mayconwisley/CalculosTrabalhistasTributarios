namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Transferencia;

/// <summary>O adicional de transferência de uma competência, arredondado nos centavos.</summary>
public sealed record AdicionalTransferenciaDoMes(MesTransferencia Mes, decimal Valor);
