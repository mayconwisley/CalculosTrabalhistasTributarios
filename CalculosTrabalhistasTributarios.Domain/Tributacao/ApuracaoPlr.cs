namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

public sealed record ApuracaoPlr(decimal BaseCalculo, int Faixa, decimal Aliquota, decimal Deducao, decimal Imposto);
