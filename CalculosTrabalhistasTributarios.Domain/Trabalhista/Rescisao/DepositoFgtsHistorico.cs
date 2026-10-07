namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>Valor de FGTS devido na competência, inclusive sobre 13º pago nela, antes do mês da rescisão.</summary>
public sealed record DepositoFgtsHistorico(DateOnly Competencia, decimal Valor);
