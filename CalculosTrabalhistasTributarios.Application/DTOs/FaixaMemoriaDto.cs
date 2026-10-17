namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Uma faixa do quadro: o intervalo, a parte da base que cai nela, a alíquota e o valor antes e depois de truncar.</summary>
/// <param name="Ate">Limite da faixa; nulo na última faixa do IRRF, que não tem limite.</param>
/// <param name="Aliquota">Percentual, como 7,5 para 7,5%.</param>
/// <param name="Valor">Valor considerado: no INSS, o produto truncado nos centavos; no IRRF, o próprio produto exato.</param>
public sealed record FaixaMemoriaDto(int Numero, decimal De, decimal? Ate, decimal ParteDaBase, decimal Aliquota, decimal ValorExato, decimal Valor);
