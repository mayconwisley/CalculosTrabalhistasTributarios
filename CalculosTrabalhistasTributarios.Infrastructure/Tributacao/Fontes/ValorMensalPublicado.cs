namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>Valor de um mês de um índice, em %.</summary>
public sealed record ValorMensalPublicado(DateOnly Competencia, decimal Valor);
