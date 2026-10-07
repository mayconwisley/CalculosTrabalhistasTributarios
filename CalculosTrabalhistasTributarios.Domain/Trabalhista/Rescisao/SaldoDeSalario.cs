namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>Os dias trabalhados no mês do desligamento e o salário deles.</summary>
/// <param name="DiasNoMes">Dias até o desligamento, no mês comercial de 30 dias.</param>
/// <param name="Dias">Dias pagos: os do mês menos as faltas.</param>
/// <param name="VerbasDoMes">Saldo, menos o DSR perdido, mais outros proventos e a quitação do banco: base do INSS, IRRF e FGTS do mês.</param>
/// <param name="DsrPerdido">Um dia de salário por semana com falta injustificada (Lei 605/1949, art. 6º).</param>
public sealed record SaldoDeSalario(int DiasNoMes, int Dias, decimal Valor, decimal VerbasDoMes, int SemanasComFalta = 0, decimal DsrPerdido = 0m);
