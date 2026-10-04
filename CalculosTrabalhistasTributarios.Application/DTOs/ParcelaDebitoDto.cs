namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="FatorCorrecao">Produto dos índices de correção mensais (IPCA-E, INPC ou IPCA), sem a Selic.</param>
/// <param name="PercentualSelic">Selic simples do período em que ela substitui a correção e os juros, em %.</param>
/// <param name="Atualizado">Valor corrigido: o valor vezes o fator de correção vezes a Selic.</param>
/// <param name="PercentualJuros">Juros simples sobre o valor atualizado: TR na fase pré-judicial trabalhista e taxa legal desde 30/08/2024, em %.</param>
public sealed record ParcelaDebitoDto(
    string Descricao,
    DateOnly Vencimento,
    decimal Valor,
    decimal FatorCorrecao,
    decimal PercentualSelic,
    decimal Atualizado,
    decimal PercentualJuros,
    decimal Juros,
    decimal Total);
