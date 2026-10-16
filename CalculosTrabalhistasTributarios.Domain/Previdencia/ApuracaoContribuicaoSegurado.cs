namespace CalculosTrabalhistasTributarios.Domain.Previdencia;

/// <summary>A contribuição de cada mês, o código e o vencimento da GPS e a complementação dos planos reduzidos.</summary>
/// <param name="Codigo">Código de pagamento da GPS para a categoria, o plano e a periodicidade.</param>
/// <param name="CodigoComplementacao">Código da complementação de 9% do plano simplificado; nulo nos demais planos.</param>
/// <param name="Vencimento">Dia 15 do mês seguinte à competência ou ao trimestre, adiado para o dia útil seguinte quando não há expediente bancário.</param>
public sealed record ApuracaoContribuicaoSegurado(
    EntradaContribuicaoSegurado Entrada,
    IReadOnlyList<ContribuicaoDoMes> Meses,
    string Codigo,
    string? CodigoComplementacao,
    DateOnly Vencimento)
{
    public decimal Total => Meses.Sum(mes => mes.Valor);
    public decimal TotalComplementacao => Meses.Sum(mes => mes.Complementacao);
    public decimal PercentualComplementacao => CalculadoraContribuicaoSegurado.AliquotaNormal - Meses[0].Aliquota;
}
