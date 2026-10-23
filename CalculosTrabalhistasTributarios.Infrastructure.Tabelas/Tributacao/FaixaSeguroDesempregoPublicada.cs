namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <param name="LimiteMedia">Limite da média salarial da faixa; o da última representa "acima de".</param>
/// <param name="Percentual">Percentual sobre a média, na 1ª faixa, ou sobre o que passa da faixa anterior, na 2ª.</param>
/// <param name="ValorFixo">Valor somado ao percentual; na última faixa, o valor máximo da parcela.</param>
public sealed record FaixaSeguroDesempregoPublicada(decimal LimiteMedia, decimal Percentual, decimal ValorFixo);
