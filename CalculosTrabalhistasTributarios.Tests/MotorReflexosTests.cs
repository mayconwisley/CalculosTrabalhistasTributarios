using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Motor de reflexos com valores conferidos à mão.</summary>
public class MotorReflexosTests
{
    private const IncidenciasReflexo Todos = IncidenciasReflexo.Dsr | IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias
        | IncidenciasReflexo.AvisoPrevio | IncidenciasReflexo.Fgts | IncidenciasReflexo.MultaFgts;

    private static MesVerbaReflexa[] Meses(int ano, int primeiro, int quantidade, decimal valor) =>
        Enumerable.Range(0, quantidade).Select(i => new MesVerbaReflexa(new DateOnly(ano, primeiro, 1).AddMonths(i), valor)).ToArray();

    [Fact]
    public void Diferenca_salarial_de_500_por_12_meses()
    {
        // O salário mensal já remunera o repouso: sem DSR. 13º = 6.000 ÷ 12 = 500; férias = 500 e o terço 166,67.
        // FGTS sobre 6.000 + 500 + 500 + 166,67 = 7.166,67: 573,33; multa de 40%: 229,33.
        var a = MotorReflexos.Calcular(new VerbaReflexa("Diferença salarial", NaturezaVerbaReflexa.Outra, true,
            IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias | IncidenciasReflexo.Fgts | IncidenciasReflexo.MultaFgts, Meses(2025, 1, 12, 500m))).Sucesso();

        Assert.Equal(6000m, a.Principal);
        Assert.Equal(0m, a.Dsr);
        Assert.Equal(500m, a.DecimoTerceiro.Single().Valor);
        Assert.Equal((500m, 166.67m), (a.Ferias, a.TercoFerias));
        Assert.Equal((7166.67m, 573.33m, 229.33m), (a.BaseFgts, a.Fgts, a.MultaFgts));
        Assert.Equal(7166.67m, a.TotalAoEmpregado);
    }

    [Fact]
    public void Dsr_das_horas_extras_so_reflete_desde_abril_de_2023()
    {
        // 300,00 de horas extras por mês, sem feriados. DSR: 02/2023 300 ÷ 24 x 4 = 50,00; 03/2023 300 ÷ 27 x 4 = 44,44;
        // 04/2023 300 ÷ 25 x 5 = 60,00; 05/2023 300 ÷ 27 x 4 = 44,44. Só os dois últimos entram na base dos reflexos.
        var a = MotorReflexos.Calcular(new VerbaReflexa("Horas extras", NaturezaVerbaReflexa.HorasExtras, true, Todos, Meses(2023, 2, 4, 300m), DiasAvisoPrevio: 30)).Sucesso();

        Assert.Equal([50m, 44.44m, 60m, 44.44m], a.Meses.Select(mes => mes.Dsr));
        Assert.Equal([false, false, true, true], a.Meses.Select(mes => mes.DsrRepercute));
        // Base reflexa: 300 + 300 + 360 + 344,44 = 1.304,44. 13º e férias: ÷ 12 = 108,70; terço 36,23.
        Assert.Equal(108.70m, a.DecimoTerceiro.Single().Valor);
        Assert.Equal((108.70m, 36.23m), (a.Ferias, a.TercoFerias));
        // Aviso de 30 dias: média dos 4 meses, 1.304,44 ÷ 4 = 326,11.
        Assert.Equal((4, 326.11m, 326.11m), (a.MesesAviso, a.MediaAviso, a.AvisoPrevio));
        // FGTS: 1.200 + 198,88 (todo o DSR) + 108,70 + 144,93 + 326,11 = 1.978,62 x 8% = 158,29; multa 63,32.
        Assert.Equal((1978.62m, 158.29m, 63.32m), (a.BaseFgts, a.Fgts, a.MultaFgts));
    }

    [Fact]
    public void Ferias_indenizadas_ficam_fora_do_fgts_e_outras_verbas_seguem_a_premissa_do_dsr()
    {
        // Adicional de 100,00 em 05/2023 (27 dias úteis e 4 de descanso): DSR de 14,81, que repercute por premissa.
        var a = MotorReflexos.Calcular(new VerbaReflexa("Adicional", NaturezaVerbaReflexa.Outra, true,
            IncidenciasReflexo.Dsr | IncidenciasReflexo.Ferias | IncidenciasReflexo.Fgts, Meses(2023, 5, 1, 100m), DsrRepercuteNasOutras: true, FeriasGozadas: false)).Sucesso();

        Assert.Equal(114.81m, a.Meses.Single().BaseReflexa);
        Assert.Equal((9.57m, 3.19m), (a.Ferias, a.TercoFerias));   // 114,81 ÷ 12 = 9,57; o terço, 3,19
        Assert.Equal(114.81m, a.BaseFgts);                          // sem as férias indenizadas
    }

    [Theory]
    [InlineData(false, IncidenciasReflexo.DecimoTerceiro, 0, "exigem que a verba")]
    [InlineData(true, IncidenciasReflexo.AvisoPrevio, 0, "de 30 a 90 dias")]
    [InlineData(true, IncidenciasReflexo.Fgts, 30, "de 30 a 90 dias")]
    [InlineData(true, IncidenciasReflexo.MultaFgts, 0, "reflexo no FGTS")]
    public void Premissas_invalidas_sao_recusadas(bool habitual, IncidenciasReflexo incidencias, int diasAviso, string trecho)
    {
        var resultado = MotorReflexos.Calcular(new VerbaReflexa("Verba", NaturezaVerbaReflexa.Outra, habitual, incidencias, Meses(2025, 1, 2, 100m), DiasAvisoPrevio: diasAviso));
        Assert.Contains(trecho, resultado.Erro.Mensagem);
    }

    [Fact]
    public void Competencia_repetida_e_recusada()
    {
        var meses = new[] { new MesVerbaReflexa(new DateOnly(2025, 1, 1), 100m), new MesVerbaReflexa(new DateOnly(2025, 1, 1), 100m) };
        Assert.Contains("deve aparecer uma vez", MotorReflexos.Calcular(new VerbaReflexa("Verba", NaturezaVerbaReflexa.Outra, true, IncidenciasReflexo.Fgts, meses)).Erro.Mensagem);
    }

    [Fact]
    public void Memoria_e_demonstrativo_usam_a_mesma_apuracao()
    {
        var a = MotorReflexos.Calcular(new VerbaReflexa("Horas extras", NaturezaVerbaReflexa.HorasExtras, true, Todos, Meses(2023, 2, 4, 300m), DiasAvisoPrevio: 30)).Sucesso();

        var proventos = MemoriaReflexos.Proventos(a);
        Assert.Equal(a.TotalAoEmpregado, proventos.Sum(verba => verba.Valor));
        Assert.Equal([158.29m, 63.32m], MemoriaReflexos.Informativos(a).Select(verba => verba.Valor));
        var formulas = MemoriaReflexos.Grupo(a).Formulas;
        Assert.Contains(formulas, formula => formula.Titulo == "DSR nos demais reflexos" && formula.Formula.Contains("Tema Repetitivo 9"));
        Assert.Contains(formulas, formula => formula.Titulo == "13º de 2023" && Texto.Normalizar(formula.Formula) == "R$ 1.304,44 (base de 4 meses) ÷ 12 = R$ 108,70");
    }
}
