namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>Os dias trabalhados no mês do desligamento e o salário deles.</summary>
/// <param name="DiasNoMes">Dias até o desligamento, no mês comercial de 30 dias.</param>
/// <param name="Dias">Dias pagos: os do mês menos as faltas.</param>
/// <param name="VerbasDoMes">Saldo mais os outros proventos do mês: a base do INSS, do IRRF e do FGTS do mês.</param>
public sealed record SaldoDeSalario(int DiasNoMes, int Dias, decimal Valor, decimal VerbasDoMes);
