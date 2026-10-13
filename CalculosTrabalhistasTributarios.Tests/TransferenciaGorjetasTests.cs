using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Gorjetas;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Transferencia;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Adicional de transferência e gorjetas, com valores conferidos à mão.</summary>
public class TransferenciaGorjetasTests
{
    private const IncidenciasReflexo Ativo = IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias | IncidenciasReflexo.Fgts;

    // Salário de origem de 4.000,00, de 03/2026 (15 dias) a 08/2026.
    private static MesTransferencia[] Transferencia() =>
        Enumerable.Range(3, 6).Select(mes => new MesTransferencia(new DateOnly(2026, mes, 1), 4000m, mes == 3 ? 15 : 30)).ToArray();

    [Fact]
    public void Adicional_de_25_por_cento_com_mes_parcial_e_reflexos()
    {
        var t = CalculadoraTransferencia.Calcular(new EntradaTransferencia(Transferencia(), 25m, Ativo, 1500m)).Sucesso();

        // 03/2026: 4.000 x 25% x 15/30 = 500,00; 04 a 08/2026: 1.000,00 cada.
        Assert.Equal([500m, 1000m, 1000m, 1000m, 1000m, 1000m], t.Meses.Select(mes => mes.Valor));
        // 13º e férias: 5.500 ÷ 12 = 458,33; terço 152,78. FGTS sobre 6.569,44 = 525,56. Sem DSR.
        Assert.Equal((458.33m, 458.33m, 152.78m, 0m), (t.Reflexos.TotalDecimoTerceiro, t.Reflexos.Ferias, t.Reflexos.TercoFerias, t.Reflexos.Dsr));
        Assert.Equal((6569.44m, 525.56m), (t.Reflexos.BaseFgts, t.Reflexos.Fgts));
        // As despesas da mudança somam ao empregado, fora do FGTS: 6.569,44 + 1.500,00.
        Assert.Equal(8069.44m, t.TotalAoEmpregado);
    }

    [Fact]
    public void Dispensa_durante_a_transferencia_reflete_no_aviso()
    {
        var incidencias = Ativo | IncidenciasReflexo.AvisoPrevio | IncidenciasReflexo.MultaFgts;
        var t = CalculadoraTransferencia.Calcular(new EntradaTransferencia(Transferencia(), 25m, incidencias, DiasAvisoPrevio: 33, FeriasGozadas: false)).Sucesso();

        // Aviso: 5.500 ÷ 6 = 916,67 x 33 ÷ 30 = 1.008,34. FGTS: 5.500 + 458,33 + 1.008,34 = 6.966,67 (sem férias) x 8% = 557,33; multa 222,93.
        Assert.Equal(1008.34m, t.Reflexos.AvisoPrevio);
        Assert.Equal((6966.67m, 557.33m, 222.93m), (t.Reflexos.BaseFgts, t.Reflexos.Fgts, t.Reflexos.MultaFgts));
    }

    [Theory]
    [InlineData(24.99, 30, IncidenciasReflexo.Fgts, "no mínimo 25%")]
    [InlineData(25, 31, IncidenciasReflexo.Fgts, "de 0 a 30")]
    [InlineData(25, 0, IncidenciasReflexo.Fgts, "pelo menos uma competência")]
    [InlineData(25, 30, IncidenciasReflexo.Fgts | IncidenciasReflexo.Dsr, "já remunera o repouso")]
    public void Transferencia_recusa_entradas_invalidas(double percentual, int dias, IncidenciasReflexo incidencias, string trecho)
    {
        var entrada = new EntradaTransferencia([new(new DateOnly(2026, 3, 1), 4000m, dias)], (decimal)percentual, incidencias);
        Assert.Contains(trecho, CalculadoraTransferencia.Calcular(entrada).Erro.Mensagem);
    }

    [Fact]
    public void Gorjetas_com_retencao_de_20_por_cento_e_media()
    {
        var meses = Enumerable.Range(5, 6).Select(mes => new MesGorjeta(new DateOnly(2026, mes, 1), 2000m)).ToArray();
        var g = CalculadoraGorjetas.Calcular(new EntradaGorjetas(meses, true, 20m, Ativo)).Sucesso();

        // 2.000 - 20% (400) = 1.600 por mês: 9.600. 13º e férias: 800,00; terço 266,67. FGTS sobre 11.466,67 = 917,33.
        Assert.Equal((12000m, 2400m, 9600m), (g.Arrecadado, g.Retido, g.Repassado));
        Assert.Equal((800m, 800m, 266.67m), (g.Reflexos.TotalDecimoTerceiro, g.Reflexos.Ferias, g.Reflexos.TercoFerias));
        Assert.Equal((11466.67m, 917.33m), (g.Reflexos.TotalAoEmpregado, g.Reflexos.Fgts));
        Assert.Equal((6, 1600m), (g.MesesMedia, g.Media));
    }

    [Fact]
    public void Media_usa_os_ultimos_12_meses_e_o_13o_e_separado_por_ano()
    {
        // 10/2025 com 1.000,00 e 11/2025 a 10/2026 com 2.000,00, sem retenção.
        var meses = Enumerable.Range(0, 13).Select(i => new MesGorjeta(new DateOnly(2025, 10, 1).AddMonths(i), i == 0 ? 1000m : 2000m)).ToArray();
        var g = CalculadoraGorjetas.Calcular(new EntradaGorjetas(meses, false, 0m, Ativo)).Sucesso();

        Assert.Equal((12, 2000m), (g.MesesMedia, g.Media));
        // 13º de 2025: 5.000 ÷ 12 = 416,67; de 2026: 20.000 ÷ 12 = 1.666,67.
        Assert.Equal([416.67m, 1666.67m], g.Reflexos.DecimoTerceiro.Select(ano => ano.Valor));
    }

    [Fact]
    public void Retencao_arredonda_nos_centavos()
    {
        var g = CalculadoraGorjetas.Calcular(new EntradaGorjetas([new(new DateOnly(2026, 5, 1), 1234.57m)], true, 20m, IncidenciasReflexo.Fgts)).Sucesso();
        Assert.Equal((246.91m, 987.66m), (g.Meses[0].Retencao, g.Meses[0].Repasse));   // 1.234,57 x 20% = 246,914
    }

    [Theory]
    [InlineData(true, 20.01, IncidenciasReflexo.Fgts, "de 0% a 20% no Simples Nacional")]
    [InlineData(false, 33.01, IncidenciasReflexo.Fgts, "de 0% a 33%")]
    [InlineData(false, 0, IncidenciasReflexo.Fgts | IncidenciasReflexo.Dsr, "Súmula 354")]
    [InlineData(false, 0, IncidenciasReflexo.Fgts | IncidenciasReflexo.AvisoPrevio, "Súmula 354")]
    public void Gorjetas_recusam_retencao_acima_do_limite_e_reflexos_da_sumula_354(bool simples, double retencao, IncidenciasReflexo incidencias, string trecho)
    {
        var entrada = new EntradaGorjetas([new(new DateOnly(2026, 5, 1), 2000m)], simples, (decimal)retencao, incidencias);
        Assert.Contains(trecho, CalculadoraGorjetas.Calcular(entrada).Erro.Mensagem);
    }

    [Fact]
    public async Task Demonstrativos_usam_a_mesma_apuracao()
    {
        var t = (await new SimularTransferenciaUseCase().ExecutarAsync(new SimularTransferenciaRequest(new EntradaTransferencia(Transferencia(), 25m, Ativo, 1500m)), default)).Sucesso();
        Assert.Equal(8069.44m, t.Resultado);
        Assert.Equal("Despesas da transferência (CLT, art. 470)", t.Proventos[^1].Descricao);
        Assert.Equal("R$ 4.000,00 x 25% x 15/30 dias = R$ 500,00", Texto.Normalizar(t.Memoria[0].Formulas[0].Formula));

        var meses = Enumerable.Range(5, 6).Select(mes => new MesGorjeta(new DateOnly(2026, mes, 1), 2000m)).ToArray();
        var g = (await new SimularGorjetasUseCase().ExecutarAsync(new SimularGorjetasRequest(new EntradaGorjetas(meses, true, 20m, Ativo)), default)).Sucesso();
        Assert.Equal(11466.67m, g.Resultado);
        Assert.Equal([2400m, 917.33m], g.Informativos.Select(verba => verba.Valor));
        Assert.Equal("R$ 2.000,00 - 20% (R$ 400,00) = R$ 1.600,00", Texto.Normalizar(g.Memoria[0].Formulas[0].Formula));
    }
}
