namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <param name="BaseHora">Salário mais os adicionais salariais, que formam o valor da hora.</param>
public sealed record HorasDoMes(
    HorasInformadas Informadas,
    decimal BaseHora,
    decimal Divisor,
    decimal Faixa1,
    decimal Faixa2,
    decimal AdicionalNoturno,
    decimal ExtrasNoturnas,
    decimal Dsr,
    int DiasUteis,
    int DiasDescanso)
{
    public decimal ValorHora => BaseHora / Divisor;

    /// <summary>Horas extras e adicional noturno, sem o DSR.</summary>
    public decimal Variaveis => Faixa1 + Faixa2 + AdicionalNoturno + ExtrasNoturnas;

    /// <summary>Horas noturnas pagas: as de relógio convertidas para a hora reduzida, ou as próprias no trabalho rural.</summary>
    public decimal HorasNoturnasReduzidas => Informadas.Rural ? Informadas.HorasNoturnas : RegrasTrabalhistas.HorasNoturnasReduzidas(Informadas.HorasNoturnas);
    public decimal HorasExtrasNoturnasReduzidas => Informadas.Rural ? Informadas.HorasExtrasNoturnas : RegrasTrabalhistas.HorasNoturnasReduzidas(Informadas.HorasExtrasNoturnas);
}
