namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <param name="Custo">Custo de aquisição, com as benfeitorias e as despesas da compra declaradas.</param>
/// <param name="DespesasVenda">Corretagem e outras despesas da venda pagas pelo vendedor.</param>
/// <param name="UnicoImovel">Único imóvel do vendedor, sem outra venda de imóvel nos 5 anos anteriores.</param>
/// <param name="Reinvestido">Valor aplicado em imóvel residencial no País em até 180 dias da venda.</param>
/// <param name="VendasNoMes">Total das vendas de bens da mesma natureza no mês, para a isenção de pequeno valor.</param>
public sealed record AlienacaoDeBem(
    BemAlienado Bem,
    DateOnly Aquisicao,
    DateOnly Alienacao,
    decimal Custo,
    decimal Venda,
    decimal DespesasVenda,
    bool UnicoImovel,
    decimal Reinvestido,
    decimal VendasNoMes)
{
    public bool Imovel => Bem is BemAlienado.ImovelResidencial or BemAlienado.OutroImovel;
}
