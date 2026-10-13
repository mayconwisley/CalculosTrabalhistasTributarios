namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Transferencia;

/// <summary>Uma competência em que o empregado esteve transferido provisoriamente.</summary>
/// <param name="Salario">Salário que o empregado recebia na localidade de origem, base do adicional (CLT, art. 469, § 3º).</param>
/// <param name="Dias">Dias do mês em que esteve transferido, de 0 a 30; 30 é o mês inteiro, inclusive em meses de 31 dias.</param>
public sealed record MesTransferencia(DateOnly Competencia, decimal Salario, int Dias = 30);
