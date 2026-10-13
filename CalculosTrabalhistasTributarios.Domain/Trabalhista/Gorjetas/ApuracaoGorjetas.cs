using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Gorjetas;

/// <summary>As gorjetas repassadas mês a mês, os reflexos e a média dos últimos 12 meses.</summary>
/// <param name="MesesMedia">Meses que formam a média: os últimos 12 informados, ou todos, se forem menos.</param>
/// <param name="Media">Média mensal das gorjetas repassadas nesses meses (CLT, art. 457, § 9º).</param>
public sealed record ApuracaoGorjetas(EntradaGorjetas Entrada, IReadOnlyList<GorjetaDoMes> Meses, ApuracaoReflexos Reflexos, int MesesMedia, decimal Media)
{
    public decimal LimiteRetencao => CalculadoraGorjetas.LimiteRetencao(Entrada.RegimeDiferenciado);
    public decimal Arrecadado => Meses.Sum(mes => mes.Mes.Valor);
    public decimal Retido => Meses.Sum(mes => mes.Retencao);
    public decimal Repassado => Meses.Sum(mes => mes.Repasse);
}
