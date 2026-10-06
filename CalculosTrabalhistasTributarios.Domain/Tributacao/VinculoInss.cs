namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

public sealed record VinculoInss(string Identificacao, TipoVinculoInss Tipo, decimal Remuneracao);
