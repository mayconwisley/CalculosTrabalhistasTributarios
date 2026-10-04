namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <param name="Faixa">Faixa da tabela em que a média caiu.</param>
/// <param name="ValorPelaTabela">Valor pela fórmula da faixa, antes do piso do salário mínimo.</param>
/// <param name="Valor">Valor de cada parcela: o da tabela, mas nunca abaixo do salário mínimo.</param>
public sealed record ParcelaSeguroDesemprego(int Faixa, decimal LimiteAnterior, decimal Percentual, decimal ValorFixo, decimal ValorPelaTabela, decimal Valor);
