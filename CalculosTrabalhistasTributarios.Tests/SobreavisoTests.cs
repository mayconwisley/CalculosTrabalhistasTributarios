using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Sobreaviso (1/3) e prontidão (2/3) com valores conferidos à mão. Salário de 3.300,00 ÷ 220 = 15,00 por hora;
/// 40 horas de sobreaviso = 200,00 e 6 de prontidão = 60,00: 260,00 por mês, de 07/2026 a 10/2026, com um feriado em
/// 09/2026 e outro em 10/2026.
/// </summary>
public class SobreavisoTests
{
    private const IncidenciasReflexo Ativo = IncidenciasReflexo.Dsr | IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias | IncidenciasReflexo.Fgts;
    private const IncidenciasReflexo Dispensa = Ativo | IncidenciasReflexo.AvisoPrevio | IncidenciasReflexo.MultaFgts;

    private static MesSobreaviso[] Meses() =>
    [
        new(new DateOnly(2026, 7, 1), 3300m, 40m, 6m),
        new(new DateOnly(2026, 8, 1), 3300m, 40m, 6m),
        new(new DateOnly(2026, 9, 1), 3300m, 40m, 6m, 1),
        new(new DateOnly(2026, 10, 1), 3300m, 40m, 6m, 1)
    ];

    [Fact]
    public void Contrato_ativo_com_dsr_fora_dos_reflexos()
    {
        var s = CalculadoraSobreaviso.Calcular(new EntradaSobreaviso(Meses(), 220m, true, Ativo)).Sucesso();

        Assert.All(s.Meses, mes => Assert.Equal((200m, 60m), (mes.ValorSobreaviso, mes.ValorProntidao)));
        // DSR: 07/2026 260 ÷ 27 x 4 = 38,52; 08/2026 260 ÷ 26 x 5 = 50,00; 09/2026 260 ÷ 25 x 5 = 52,00; 10/2026 260 ÷ 26 x 5 = 50,00.
        Assert.Equal([38.52m, 50m, 52m, 50m], s.Reflexos.Meses.Select(mes => mes.Dsr));
        // 13º e férias: 1.040 ÷ 12 = 86,67; terço 28,89. Total: 1.040 + 190,52 + 86,67 + 86,67 + 28,89 = 1.432,75.
        Assert.Equal((86.67m, 86.67m, 28.89m), (s.Reflexos.TotalDecimoTerceiro, s.Reflexos.Ferias, s.Reflexos.TercoFerias));
        Assert.Equal(1432.75m, s.Reflexos.TotalAoEmpregado);
        Assert.Equal((1432.75m, 114.62m, 0m), (s.Reflexos.BaseFgts, s.Reflexos.Fgts, s.Reflexos.MultaFgts));
    }

    [Fact]
    public void Dispensa_com_aviso_de_33_dias_e_ferias_indenizadas()
    {
        var s = CalculadoraSobreaviso.Calcular(new EntradaSobreaviso(Meses(), 220m, true, Dispensa, DiasAvisoPrevio: 33, FeriasGozadas: false)).Sucesso();

        // Aviso: média de 260,00 x 33 ÷ 30 = 286,00. FGTS: 1.230,52 + 86,67 + 286,00 = 1.603,19 (sem as férias) x 8% = 128,26; multa 51,30.
        Assert.Equal(286m, s.Reflexos.AvisoPrevio);
        Assert.Equal((1603.19m, 128.26m, 51.30m), (s.Reflexos.BaseFgts, s.Reflexos.Fgts, s.Reflexos.MultaFgts));
    }

    [Fact]
    public void Dsr_nos_reflexos_por_premissa()
    {
        var s = CalculadoraSobreaviso.Calcular(new EntradaSobreaviso(Meses(), 220m, true, Ativo, DsrRepercuteNasOutras: true)).Sucesso();
        Assert.Equal(102.54m, s.Reflexos.TotalDecimoTerceiro);   // (1.040 + 190,52) ÷ 12
    }

    [Fact]
    public void Horas_eventuais_tem_so_o_fgts_e_minutos_contam()
    {
        // 40:30 h de sobreaviso: 15,00 x 40,5 ÷ 3 = 202,50; sem prontidão.
        var s = CalculadoraSobreaviso.Calcular(new EntradaSobreaviso([new(new DateOnly(2026, 7, 1), 3300m, 40.5m, 0m)], 220m, false, IncidenciasReflexo.Fgts)).Sucesso();

        Assert.Equal("Horas de sobreaviso", s.Reflexos.Verba.Descricao);
        Assert.Equal(202.50m, s.Reflexos.TotalAoEmpregado);
        Assert.Equal(16.20m, s.Reflexos.Fgts);
    }

    [Theory]
    [InlineData(0, 3300, 40, 0, "divisor")]
    [InlineData(220, 0, 40, 0, "salário de 07/2026")]
    [InlineData(220, 3300, 700, 50, "passam das 744 horas")]
    [InlineData(220, 3300, 0, 0, "pelo menos uma competência")]
    [InlineData(220, 3300, -1, 0, "não podem ser negativas")]
    public void Entradas_invalidas_sao_recusadas(int divisor, int salario, int sobreaviso, int prontidao, string trecho)
    {
        var entrada = new EntradaSobreaviso([new(new DateOnly(2026, 7, 1), salario, sobreaviso, prontidao)], divisor, true, Ativo);
        Assert.Contains(trecho, CalculadoraSobreaviso.Calcular(entrada).Erro.Mensagem);
    }

    [Fact]
    public async Task Demonstrativo_separa_as_horas_e_soma_os_reflexos()
    {
        var d = (await new SimularSobreavisoUseCase().ExecutarAsync(new SimularSobreavisoRequest(new EntradaSobreaviso(Meses(), 220m, true, Ativo)), default)).Sucesso();

        Assert.Equal([("Horas de sobreaviso (1/3)", 800m), ("Horas de prontidão (2/3)", 240m)], d.Proventos.Take(2).Select(verba => (verba.Descricao, verba.Valor)));
        Assert.Equal(1432.75m, d.Resultado);
        Assert.Equal([114.62m], d.Informativos.Select(verba => verba.Valor));
        var formula = d.Memoria[0].Formulas[0].Formula;
        Assert.Equal("sobreaviso: R$ 3.300,00 ÷ 220 x 40:00 h x 1/3 = R$ 200,00; prontidão: R$ 3.300,00 ÷ 220 x 6:00 h x 2/3 = R$ 60,00", Texto.Normalizar(formula));
    }
}
