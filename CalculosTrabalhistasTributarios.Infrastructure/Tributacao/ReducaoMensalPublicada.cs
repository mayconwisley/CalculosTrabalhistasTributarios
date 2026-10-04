namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

public sealed record ReducaoMensalPublicada(int Faixa, decimal LimiteRendimentos, decimal Multiplicador, decimal ValorBase);
