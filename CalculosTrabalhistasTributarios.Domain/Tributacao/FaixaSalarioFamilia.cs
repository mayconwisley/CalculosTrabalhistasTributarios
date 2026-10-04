namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Faixa do salário-família: a cota por filho para quem recebe até o limite de remuneração.</summary>
public sealed record FaixaSalarioFamilia(int Faixa, decimal LimiteRemuneracao, decimal Cota);
