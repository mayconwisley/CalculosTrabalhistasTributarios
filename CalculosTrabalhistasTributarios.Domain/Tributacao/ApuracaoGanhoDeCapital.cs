namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <param name="Ganho">Venda menos as despesas da venda e o custo; zero quando houve perda.</param>
/// <param name="Isencao">Motivo da isenção total; nulo quando há imposto a apurar.</param>
/// <param name="PercentualReducao1988">Redução dos imóveis comprados até 1988 (Lei 7.713/1988, art. 18).</param>
/// <param name="Fr1">Fator de redução de 01/1996 a 11/2005; 1 quando não se aplica.</param>
/// <param name="Fr2">Fator de redução de 12/2005 até a venda; 1 quando não se aplica.</param>
/// <param name="ProporcaoReinvestida">Parte da venda aplicada em imóvel residencial em 180 dias, que fica isenta.</param>
/// <param name="Faixas">Imposto de cada faixa progressiva: a parcela do ganho, a alíquota e o imposto.</param>
public sealed record ApuracaoGanhoDeCapital(
    decimal Ganho,
    string? Isencao,
    decimal PercentualReducao1988,
    int MesesFr1,
    decimal Fr1,
    int MesesFr2,
    decimal Fr2,
    decimal ProporcaoReinvestida,
    decimal GanhoTributavel,
    IReadOnlyList<(decimal Parcela, decimal Aliquota, decimal Imposto)> Faixas)
{
    public decimal Imposto => Faixas.Sum(faixa => faixa.Imposto);
}
