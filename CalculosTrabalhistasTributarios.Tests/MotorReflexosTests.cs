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

    // 500,00 por mês de 01/2024 a 06/2026, com admissão em 10/03/2023: o período de 10/03/2023 tem 2 meses (01 e 02/2024), os de
    // 10/03/2024 e 10/03/2025 têm 12 cada, e o de 10/03/2026 está em curso, com 4 meses (03 a 06/2026). Referência: 30/06/2026.
    private static VerbaReflexa VerbaComAdmissao(bool contratoAtivo) => new("Adicional", NaturezaVerbaReflexa.Outra, true,
        IncidenciasReflexo.Ferias | IncidenciasReflexo.Fgts, Meses(2024, 1, 30, 500m), FeriasGozadas: contratoAtivo, Admissao: new DateOnly(2023, 3, 10));

    [Fact]
    public void Contrato_ativo_nao_deve_as_ferias_do_periodo_em_curso()
    {
        var a = MotorReflexos.Calcular(VerbaComAdmissao(contratoAtivo: true)).Sucesso();

        // 1.000 ÷ 12 = 83,33 (terço 27,78); 6.000 ÷ 12 = 500 (166,67) duas vezes; o período em curso, 2.000 ÷ 12 = 166,67 (55,56), não é devido.
        Assert.Equal([(83.33m, 27.78m, SituacaoFeriasReflexo.Gozadas), (500m, 166.67m, SituacaoFeriasReflexo.Gozadas), (500m, 166.67m, SituacaoFeriasReflexo.Gozadas), (166.67m, 55.56m, SituacaoFeriasReflexo.NaoDevidas)],
            a.PeriodosFerias.Select(p => (p.Valor, p.Terco, p.Situacao)));
        Assert.Equal([new DateOnly(2023, 3, 10), new DateOnly(2024, 3, 10), new DateOnly(2025, 3, 10), new DateOnly(2026, 3, 10)], a.PeriodosFerias.Select(p => p.Inicio));
        // Período em curso: de 10/03 a 30/06/2026, 3 meses completos e 21 dias: 4/12.
        Assert.Equal(4, a.PeriodosFerias[^1].Avos);
        Assert.Equal((1083.33m, 361.12m), (a.Ferias, a.TercoFerias));
        // FGTS: 15.000 + férias gozadas 1.444,45 = 16.444,45 x 8% = 1.315,56.
        Assert.Equal((16444.45m, 1315.56m), (a.BaseFgts, a.Fgts));
    }

    [Fact]
    public void Dispensa_indeniza_vencidas_e_proporcionais_e_mantem_o_fgts_das_gozadas()
    {
        var a = MotorReflexos.Calcular(VerbaComAdmissao(contratoAtivo: false)).Sucesso();

        // O período de 10/03/2025 terminou em 09/03/2026 e ainda estava no prazo de concessão (até 09/03/2027): vencidas.
        // O de 10/03/2024 teve o prazo encerrado em 09/03/2026: presumem-se gozadas. O em curso: proporcionais.
        Assert.Equal([SituacaoFeriasReflexo.Gozadas, SituacaoFeriasReflexo.Gozadas, SituacaoFeriasReflexo.Indenizadas, SituacaoFeriasReflexo.Indenizadas], a.PeriodosFerias.Select(p => p.Situacao));
        Assert.Equal((1250m, 416.68m), (a.Ferias, a.TercoFerias));
        // FGTS: 15.000 + as gozadas (83,33 + 27,78 + 500 + 166,67 = 777,78) = 15.777,78 x 8% = 1.262,22; antes, sem as férias: 1.200,00.
        Assert.Equal((15777.78m, 1262.22m), (a.BaseFgts, a.Fgts));
    }

    [Fact]
    public void Demonstrativo_mostra_cada_periodo_e_o_em_curso_como_informativo()
    {
        var a = MotorReflexos.Calcular(VerbaComAdmissao(contratoAtivo: true)).Sucesso();

        Assert.Contains(MemoriaReflexos.Proventos(a), v => v.Descricao == "Reflexo nas férias gozadas" && Texto.Normalizar(v.Referencia) == "Desde 10/03/2024" && v.Valor == 500m);
        Assert.DoesNotContain(MemoriaReflexos.Proventos(a), v => v.Descricao.Contains("em aquisição"));
        Assert.Contains(MemoriaReflexos.Informativos(a), v => v.Descricao.StartsWith("Férias + 1/3 do período em curso") && v.Valor == 222.23m);
        Assert.Contains("período em curso (4/12)", Texto.Normalizar(MemoriaReflexos.Grupo(a).Formulas.Single(f => f.Titulo == "Férias de 10/03/2026 a 09/03/2027").Formula));
        Assert.StartsWith("Férias por período aquisitivo", MemoriaReflexos.ObservacaoFerias(a));
    }

    [Fact]
    public void Sem_admissao_mantem_um_so_periodo_e_avisa()
    {
        var a = MotorReflexos.Calcular(VerbaComAdmissao(contratoAtivo: true) with { Admissao = null }).Sucesso();
        Assert.Equal((1250m, 416.67m, false), (a.Ferias, a.TercoFerias, a.PeriodosFerias.Single().Separado));   // 15.000 ÷ 12
        Assert.StartsWith("Sem a data de admissão", MemoriaReflexos.ObservacaoFerias(a));
    }

    [Fact]
    public void Verba_antes_da_admissao_e_recusada()
    {
        var verba = VerbaComAdmissao(contratoAtivo: true) with { Admissao = new DateOnly(2024, 2, 10) };
        Assert.Contains("começa antes da admissão em 10/02/2024", MotorReflexos.Calcular(verba).Erro.Mensagem);
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
