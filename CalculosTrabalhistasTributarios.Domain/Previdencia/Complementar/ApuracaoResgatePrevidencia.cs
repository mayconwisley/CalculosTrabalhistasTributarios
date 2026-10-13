namespace CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;

/// <summary>O resgate por aporte, com o IR dos dois regimes para comparação.</summary>
/// <param name="ImpostoRegressivo">Soma do IR de cada parcela pela tabela regressiva: definitivo.</param>
/// <param name="ImpostoProgressivo">15% da base retidos no resgate: antecipação ajustada na declaração.</param>
public sealed record ApuracaoResgatePrevidencia(EntradaResgatePrevidencia Entrada, IReadOnlyList<ParcelaResgatada> Parcelas, decimal ImpostoRegressivo, decimal ImpostoProgressivo)
{
    public decimal Resgatado => Parcelas.Sum(parcela => parcela.Resgatado);
    public decimal Base => Parcelas.Sum(parcela => parcela.Base);
    public decimal SaldoTotal => Entrada.Aportes.Sum(aporte => aporte.Saldo);
    public decimal SaldoRestante => SaldoTotal - Resgatado;

    /// <summary>O IR do regime escolhido: retido no resgate.</summary>
    public decimal Imposto => Entrada.Regime == RegimeTributacaoPrevidencia.Regressivo ? ImpostoRegressivo : ImpostoProgressivo;
    public decimal Liquido => Resgatado - Imposto;
}
