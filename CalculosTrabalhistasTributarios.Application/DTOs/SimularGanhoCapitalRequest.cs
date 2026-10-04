using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Custo">Custo de aquisição declarado, com as benfeitorias.</param>
/// <param name="DespesasVenda">Corretagem e outras despesas da venda pagas pelo vendedor.</param>
/// <param name="UnicoImovel">Único imóvel do vendedor, sem outra venda de imóvel nos 5 anos anteriores.</param>
/// <param name="Reinvestido">Valor aplicado em imóvel residencial no País em até 180 dias da venda.</param>
/// <param name="VendasNoMes">Total das vendas de bens da mesma natureza no mês; zero considera só esta venda.</param>
public sealed record SimularGanhoCapitalRequest(BemAlienado Bem, DateOnly Aquisicao, DateOnly Alienacao, decimal Custo, decimal Venda, decimal DespesasVenda, bool UnicoImovel, decimal Reinvestido, decimal VendasNoMes);
