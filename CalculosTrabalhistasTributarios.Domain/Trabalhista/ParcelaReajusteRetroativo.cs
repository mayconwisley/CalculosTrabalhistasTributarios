namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public enum TipoParcelaReajuste { Salario, DecimoTerceiro, FeriasGozadas }

/// <param name="Quantidade">1 para salário mensal; avos para 13º; dias para férias gozadas.</param>
public sealed record ParcelaReajusteRetroativo(
    DateOnly Competencia, TipoParcelaReajuste Tipo, decimal BasePaga, decimal BaseDevida, int Quantidade = 1);

public sealed record DiferencaParcelaReajuste(
    ParcelaReajusteRetroativo Parcela, decimal ValorPago, decimal ValorDevido, decimal Diferenca, decimal Fgts);

public sealed record ApuracaoReajusteRetroativo(IReadOnlyList<DiferencaParcelaReajuste> Parcelas)
{
    public decimal Salario => Parcelas.Where(item => item.Parcela.Tipo == TipoParcelaReajuste.Salario).Sum(item => item.Diferenca);
    public decimal DecimoTerceiro => Parcelas.Where(item => item.Parcela.Tipo == TipoParcelaReajuste.DecimoTerceiro).Sum(item => item.Diferenca);
    public decimal Ferias => Parcelas.Where(item => item.Parcela.Tipo == TipoParcelaReajuste.FeriasGozadas).Sum(item => item.Diferenca);
    public decimal TotalBruto => Parcelas.Sum(item => item.Diferenca);
    public decimal Fgts => Parcelas.Sum(item => item.Fgts);
}
