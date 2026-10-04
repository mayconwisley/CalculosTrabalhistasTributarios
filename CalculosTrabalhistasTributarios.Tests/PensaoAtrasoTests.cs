using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class PensaoAtrasoTests
{
    // Taxa legal publicada pelo Banco Central (série SGS 29543), em % ao mês, e INPC do IBGE, para o recálculo independente.
    private static readonly Dictionary<(int Ano, int Mes), decimal> TaxaLegal = Serie(
        (2024, 8, "0.605306 0.676227 0.704241 0.385874 0.171924"),
        (2025, 1, "0.589427 0.902209 0 0.321969 0.6232 0.775982 0.83488 0.942622 1.305984 0.736394 1.093764 0.851001"),
        (2026, 1, "0.96751 0.962232 0.155714 0.768672 0.198293 0.450641 0.706607 1.154527 1.499119 0.37902"));

    private static readonly Dictionary<(int Ano, int Mes), decimal> Inpc = Serie(
        (2024, 1, "0.57 0.81 0.19 0.37 0.46 0.25 0.26 -0.14 0.48 0.61 0.33 0.48"),
        (2025, 1, "0.00 1.48 0.51 0.48 0.35 0.23 0.21 -0.21 0.52 0.03 0.03 0.21"),
        (2026, 1, "0.39 0.56 0.91 0.81 0.65 0.14 -0.01 -0.32"));

    private static async Task<SimularPensaoAtrasoUseCase> SimuladorAsync() => new(await Ambiente.ConsultaAsync(), await Ambiente.IndicesAsync());

    private static SimularPensaoAtrasoRequest Uma(DateOnly vencimento, DateOnly calculo, JurosDeMora juros) =>
        new([new ParcelaPensaoInformada(new DateOnly(vencimento.Year, vencimento.Month, 1), vencimento, 1000m, 0m)], calculo, null, CorrecaoMonetaria.Nenhuma, juros);

    [Theory]
    [InlineData("2026-08-10", "2026-10-03", 2.317897, 23.18)]
    [InlineData("2026-09-10", "2026-10-03", 1.036092, 10.36)]
    [InlineData("2024-09-30", "2024-12-03", 1.106753, 11.07)]
    public async Task Taxa_legal_e_proporcional_aos_dias_corridos_de_cada_mes(string vencimento, string calculo, decimal percentual, decimal juros)
    {
        var resultado = await (await SimuladorAsync()).CalcularAsync(Uma(DateOnly.Parse(vencimento), DateOnly.Parse(calculo), JurosDeMora.TaxaLegal), default).Sucesso();
        Assert.Equal(percentual, resultado.Parcelas[0].PercentualJuros);
        Assert.Equal(juros, resultado.Parcelas[0].Juros);
    }

    [Fact]
    public async Task Um_por_cento_vai_ate_29_de_agosto_de_2024_e_depois_vale_a_taxa_legal()
    {
        var resultado = await (await SimuladorAsync()).CalcularAsync(Uma(new DateOnly(2024, 8, 10), new DateOnly(2024, 9, 10), JurosDeMora.UmPorCentoAteTaxaLegal), default).Sucesso();
        // 19 dias a 1% ao mês (dias ÷ 30) + 2/31 da taxa de agosto (dias 30 e 31) + 10/30 da de setembro.
        var esperado = Math.Round(19m / 30m, 6) + Math.Round(0.605306m * 2m / 31m, 6) + Math.Round(0.676227m * 10m / 30m, 6);
        Assert.Equal(esperado, resultado.Parcelas[0].PercentualJuros);
    }

    [Fact]
    public async Task Taxa_legal_avisa_quando_a_parcela_e_anterior_a_lei()
    {
        var resultado = await (await SimuladorAsync()).CalcularAsync(Uma(new DateOnly(2024, 6, 10), new DateOnly(2024, 9, 10), JurosDeMora.TaxaLegal), default).Sucesso();
        Assert.Equal(Math.Round(0.605306m * 2m / 31m, 6) + Math.Round(0.676227m * 10m / 30m, 6), resultado.Parcelas[0].PercentualJuros);
        Assert.Contains(resultado.Observacoes, observacao => observacao.Contains("só existe desde 30/08/2024"));
    }

    [Fact]
    public async Task Mes_sem_taxa_legal_repete_a_ultima_com_aviso()
    {
        var resultado = await (await SimuladorAsync()).CalcularAsync(Uma(new DateOnly(2026, 9, 10), new DateOnly(2026, 11, 15), JurosDeMora.TaxaLegal), default).Sucesso();
        var esperado = Math.Round(1.499119m * 20m / 30m, 6) + 0.37902m + Math.Round(0.37902m * 15m / 30m, 6);
        Assert.Equal(esperado, resultado.Parcelas[0].PercentualJuros);
        Assert.Contains(resultado.Observacoes, observacao => observacao.Contains("11/2026 ainda não está na tabela"));
    }

    [Fact]
    public async Task Debito_de_33_parcelas_bate_com_o_recalculo_independente()
    {
        var simulador = await SimuladorAsync();
        var calculo = new DateOnly(2026, 10, 3);
        var parcelas = await simulador.GerarParcelasAsync(new GerarParcelasAtrasoRequest(BasePensao.ValorFixo, 1000m, 0m, new DateOnly(2024, 1, 1), new DateOnly(2026, 9, 1), 10), default).Sucesso();
        var resultado = await simulador.CalcularAsync(new SimularPensaoAtrasoRequest(parcelas, calculo, null, CorrecaoMonetaria.Inpc, JurosDeMora.UmPorCentoAteTaxaLegal), default).Sucesso();

        Assert.Equal(33, resultado.Parcelas.Count);
        foreach (var parcela in resultado.Parcelas)
        {
            // INPC do mês do vencimento até o último publicado (08/2026), com piso 1.
            var fator = 1m;
            for (var mes = new DateOnly(parcela.Vencimento.Year, parcela.Vencimento.Month, 1); mes <= new DateOnly(2026, 8, 1); mes = mes.AddMonths(1))
                fator *= 1m + Inpc[(mes.Year, mes.Month)] / 100m;
            var corrigido = Math.Round(1000m * Math.Max(1m, fator), 2, MidpointRounding.AwayFromZero);
            var fim1 = calculo < new DateOnly(2024, 8, 29) ? calculo : new DateOnly(2024, 8, 29);
            var percentual = (fim1 > parcela.Vencimento ? Math.Round((fim1.DayNumber - parcela.Vencimento.DayNumber) / 30m, 6) : 0m)
                + TaxaLegalEntre(parcela.Vencimento > new DateOnly(2024, 8, 29) ? parcela.Vencimento : new DateOnly(2024, 8, 29), calculo);
            Assert.Equal(corrigido, parcela.Corrigido);
            Assert.Equal(percentual, parcela.PercentualJuros);
            Assert.Equal(Math.Round(corrigido * percentual / 100m, 2, MidpointRounding.AwayFromZero), parcela.Juros);
        }
        Assert.Equal(4570.05m, resultado.TotalJuros);
    }

    [Fact]
    public async Task Ritos_separam_as_tres_ultimas_parcelas_anteriores_ao_ajuizamento_e_as_seguintes()
    {
        var simulador = await SimuladorAsync();
        var parcelas = await simulador.GerarParcelasAsync(new GerarParcelasAtrasoRequest(BasePensao.ValorFixo, 1000m, 0m, new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 1), 10), default).Sucesso();
        var resultado = await simulador.CalcularAsync(new SimularPensaoAtrasoRequest(parcelas, new DateOnly(2026, 10, 3), new DateOnly(2026, 5, 20), CorrecaoMonetaria.Nenhuma, JurosDeMora.Nenhum), default).Sucesso();
        Assert.Equal(["03/2026", "04/2026", "05/2026", "06/2026", "07/2026", "08/2026", "09/2026"],
            resultado.Parcelas.Where(parcela => parcela.RitoPrisao).Select(parcela => parcela.Competencia.ToString("MM/yyyy")));
    }

    [Theory]
    [InlineData(AcrescimosPenhora.MultaEHonorarios, 400.00, 400.00)]
    [InlineData(AcrescimosPenhora.Multa, 400.00, 0.00)]
    [InlineData(AcrescimosPenhora.Nenhum, 0.00, 0.00)]
    public async Task Multa_e_honorarios_de_10_por_cento_so_no_rito_da_penhora(AcrescimosPenhora acrescimos, decimal multa, decimal honorarios)
    {
        // 7 parcelas de 1.000,00 sem correção nem juros: as 3 últimas são do rito da prisão e as 4 primeiras, da penhora.
        var simulador = await SimuladorAsync();
        var parcelas = await simulador.GerarParcelasAsync(new GerarParcelasAtrasoRequest(BasePensao.ValorFixo, 1000m, 0m, new DateOnly(2026, 3, 1), new DateOnly(2026, 9, 1), 10), default).Sucesso();
        var resultado = await simulador.CalcularAsync(new SimularPensaoAtrasoRequest(parcelas, new DateOnly(2026, 10, 3), null, CorrecaoMonetaria.Nenhuma, JurosDeMora.Nenhum, acrescimos), default).Sucesso();

        Assert.Equal(4000m, resultado.TotalPenhora);
        Assert.Equal(multa, resultado.Multa);
        Assert.Equal(honorarios, resultado.Honorarios);
        Assert.Equal(7000m + multa + honorarios, resultado.TotalComAcrescimos);
        Assert.Equal(3000m, resultado.TotalPrisao);
    }

    [Fact]
    public async Task Sem_debito_na_penhora_a_multa_fica_em_zero_com_aviso()
    {
        var simulador = await SimuladorAsync();
        var parcelas = await simulador.GerarParcelasAsync(new GerarParcelasAtrasoRequest(BasePensao.ValorFixo, 1000m, 0m, new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 1), 10), default).Sucesso();
        var resultado = await simulador.CalcularAsync(new SimularPensaoAtrasoRequest(parcelas, new DateOnly(2026, 10, 3), null, CorrecaoMonetaria.Nenhuma, JurosDeMora.Nenhum, AcrescimosPenhora.MultaEHonorarios), default).Sucesso();

        Assert.False(resultado.TemAcrescimos);
        Assert.Contains(resultado.Observacoes, observacao => observacao.Contains("não se aplicam ao rito da prisão"));
    }

    [Fact]
    public async Task Gera_parcelas_pelo_salario_minimo_de_cada_mes_desde_2015()
    {
        var parcelas = await (await SimuladorAsync()).GerarParcelasAsync(new GerarParcelasAtrasoRequest(BasePensao.SalarioMinimo, 0m, 150m, new DateOnly(2015, 12, 1), new DateOnly(2016, 2, 1), 31), default).Sucesso();
        Assert.Equal([1182.00m, 1320.00m, 1320.00m], parcelas.Select(parcela => parcela.Devido));
        Assert.Equal(new DateOnly(2016, 2, 29), parcelas[2].Vencimento);
    }

    private static decimal TaxaLegalEntre(DateOnly inicio, DateOnly fim)
    {
        var total = 0m;
        for (var dia = inicio.AddDays(1); dia <= fim;)
        {
            var dias = DateTime.DaysInMonth(dia.Year, dia.Month);
            var ultimo = new DateOnly(dia.Year, dia.Month, dias);
            var ate = fim < ultimo ? fim : ultimo;
            total += Math.Round(TaxaLegal[(dia.Year, dia.Month)] * (ate.DayNumber - dia.DayNumber + 1) / dias, 6, MidpointRounding.AwayFromZero);
            dia = ate.AddDays(1);
        }
        return total;
    }

    private static Dictionary<(int Ano, int Mes), decimal> Serie(params (int Ano, int MesInicial, string Valores)[] anos) => anos
        .SelectMany(ano => ano.Valores.Split(' ').Select((valor, indice) => (Chave: (ano.Ano, ano.MesInicial + indice), Valor: decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture))))
        .ToDictionary(item => item.Chave, item => item.Valor);
}
