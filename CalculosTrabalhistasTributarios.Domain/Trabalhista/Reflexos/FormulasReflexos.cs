using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <summary>
/// As fórmulas de reflexo usadas pelas calculadoras, num só lugar, cada uma arredondada nos centavos como no
/// demonstrativo. O motor de reflexos e as calculadoras de horas extras, comissões e reajuste retroativo as compartilham.
/// </summary>
public static class FormulasReflexos
{
    public const decimal AliquotaFgts = 8m;
    public const decimal PercentualMultaFgts = 40m;

    /// <summary>
    /// Repouso semanal remunerado de uma verba variável: o valor dos dias úteis estendido aos dias de descanso
    /// (Lei 605/1949, art. 7º; Súmula 172 do TST): valor ÷ dias úteis × dias de descanso.
    /// </summary>
    public static decimal Dsr(decimal valor, int diasDescanso, int diasUteis) =>
        CalculadoraTributacao.Arredondar(valor * diasDescanso / diasUteis);

    /// <summary>Avos do 13º: 1/12 por mês de serviço (Lei 4.090/1962, art. 1º, § 1º): base × avos ÷ 12.</summary>
    public static decimal DecimoTerceiro(decimal baseMensal, int avos) => CalculadoraTributacao.Arredondar(baseMensal * avos / 12m);

    /// <summary>Férias de <paramref name="dias"/> dias pela base mensal, com o terço constitucional, cada parte arredondada.</summary>
    public static (decimal Ferias, decimal Terco) FeriasComTerco(decimal baseMensal, int dias)
    {
        var ferias = CalculadoraTributacao.Arredondar(baseMensal * dias / 30m);
        return (ferias, Terco(ferias));
    }

    /// <summary>O terço constitucional das férias (CF, art. 7º, XVII).</summary>
    public static decimal Terco(decimal ferias) => CalculadoraTributacao.Arredondar(ferias / 3m);

    /// <summary>Depósito do FGTS de 8% (Lei 8.036/1990, art. 15).</summary>
    public static decimal Fgts(decimal remuneracao) => CalculadoraTributacao.Arredondar(remuneracao * AliquotaFgts / 100m);

    /// <summary>Multa de 40% sobre os depósitos do FGTS na dispensa sem justa causa (Lei 8.036/1990, art. 18, § 1º).</summary>
    public static decimal MultaFgts(decimal depositos) => CalculadoraTributacao.Arredondar(depositos * PercentualMultaFgts / 100m);
}
