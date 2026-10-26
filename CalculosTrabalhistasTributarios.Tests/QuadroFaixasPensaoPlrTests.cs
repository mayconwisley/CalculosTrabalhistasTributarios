using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Infrastructure.Reporting;
using ClosedXML.Excel;
using System.IO;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Faixas em tabela na PLR e na pensão, com valores calculados à mão. A tabela anual da PLR vigente desde 05/2025 vai até
/// 8.214,40 / 9.922,28 / 13.167,00 / 16.380,38; o IRRF mensal, até 2.428,80 / 2.826,65 / 3.751,05 / 4.664,68; o INSS de
/// 2026, até 1.621,00 / 2.902,84 / 4.354,27 / 8.475,55.
/// </summary>
public class QuadroFaixasPensaoPlrTests
{
    [Fact]
    public async Task Plr_mostra_a_tabela_anual_faixa_a_faixa()
    {
        var demonstrativo = await new SimularPlrUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularPlrRequest(new DateOnly(2026, 10, 1), 20_000m, 0m, 0m), TestContext.Current.CancellationToken).Sucesso();

        var grupo = Assert.Single(demonstrativo.Memoria, item => item.Titulo == "IRRF do ano por faixas da tabela anual");
        Assert.Equal("IRRF do ano: R$ 2.333,20", Texto.Normalizar(grupo.Destaque));
        var quadro = Assert.IsType<QuadroFaixasDto>(grupo.Quadro);
        // 8.214,40 isentos; 1.707,88 x 7,5% = 128,091; 3.244,72 x 15% = 486,708; 3.213,38 x 22,5% = 723,0105; 3.619,62 x 27,5% = 995,3955.
        Assert.Equal([8_214.40m, 1_707.88m, 3_244.72m, 3_213.38m, 3_619.62m], quadro.Faixas.Select(faixa => faixa.ParteDaBase));
        Assert.Equal([0m, 128.091m, 486.708m, 723.0105m, 995.3955m], quadro.Faixas.Select(faixa => faixa.ValorExato));
        Assert.Null(quadro.Faixas[^1].Ate);
        Assert.Equal(16_380.39m, quadro.Faixas[^1].De);
        Assert.Equal(20_000m, quadro.BaseCalculo);
        Assert.Equal(2_333.205m, quadro.ValorExato);
        Assert.Equal(2_333.21m, quadro.Valor);

        // A soma das faixas arredondada difere em um centavo da parcela a deduzir: 20.000,00 x 27,5% - 3.166,80 = 2.333,20.
        var formulas = grupo.Formulas.ToDictionary(formula => formula.Titulo, formula => Texto.Normalizar(formula.Formula));
        Assert.Equal("Parte de R$ 9.922,29 a R$ 13.167,00: R$ 13.167,00 - R$ 9.922,28 = R$ 3.244,72; R$ 3.244,72 x 15% = R$ 486,708", formulas["Faixa 3 (15%)"]);
        Assert.StartsWith("R$ 20.000,00 x 27,5% - R$ 3.166,80 = R$ 2.333,20.", formulas["Pela parcela a deduzir"]);
        Assert.Contains("A soma das faixas dá R$ 2.333,21", formulas["Pela parcela a deduzir"]);
        Assert.Equal(2_333.20m, demonstrativo.Descontos.Sum(verba => verba.Valor));
    }

    [Fact]
    public async Task Plr_isenta_tem_so_a_primeira_faixa()
    {
        var consulta = await Ambiente.ConsultaAsync();
        var isenta = await new SimularPlrUseCase(consulta).ExecutarAsync(new SimularPlrRequest(new DateOnly(2026, 10, 1), 5_000m, 0m, 0m), TestContext.Current.CancellationToken).Sucesso();
        var quadro = Assert.IsType<QuadroFaixasDto>(Assert.Single(isenta.Memoria, item => item.Quadro is not null).Quadro);
        Assert.Equal([5_000m, 0m, 0m, 0m, 0m], quadro.Faixas.Select(faixa => faixa.ParteDaBase));
        Assert.Equal(0m, quadro.Valor);
    }

    [Fact]
    public async Task Pensao_traz_as_faixas_do_inss_e_do_irrf_da_ultima_iteracao()
    {
        var simulacao = await PensaoFixaDeMilAsync();

        // INSS de 6.000,00: 1.621,00 x 7,5% + 1.281,84 x 9% + 1.451,43 x 12% + 1.645,73 x 14%, cada faixa truncada.
        var inss = Assert.IsType<QuadroFaixasDto>(simulacao.QuadroInss);
        Assert.Equal([1_621.00m, 1_281.84m, 1_451.43m, 1_645.73m], inss.Faixas.Select(faixa => faixa.ParteDaBase));
        Assert.Equal(230.4022m, inss.Faixas[3].ValorExato);
        Assert.Equal(230.40m, inss.Faixas[3].Valor);
        Assert.Equal(641.50m, inss.Valor);
        Assert.Equal(simulacao.ValorInss, inss.Valor);

        // Deduções legais: 6.000,00 - 641,50 (INSS) - 1.000,00 (pensão) = 4.358,50, que termina na faixa 4.
        // 397,85 x 7,5% = 29,83875; 924,40 x 15% = 138,66; 607,45 x 22,5% = 136,67625; soma 305,175, arredondada para 305,18.
        var normal = Assert.IsType<QuadroFaixasDto>(simulacao.Normal.QuadroIrrf);
        Assert.Equal(4_358.50m, normal.BaseCalculo);
        Assert.Equal([2_428.80m, 397.85m, 924.40m, 607.45m, 0m], normal.Faixas.Select(faixa => faixa.ParteDaBase));
        Assert.Equal(305.175m, normal.ValorExato);
        Assert.Equal(305.18m, normal.Valor);
        // Pela parcela a deduzir: 4.358,50 x 22,5% - 675,49 = 305,1725, arredondado para 305,17; vale esse valor.
        Assert.Equal(305.17m, simulacao.Normal.ImpostoAntesReducao);
        Assert.StartsWith("A soma das faixas dá R$ 305,18, e o IR progressivo pela parcela a deduzir, R$ 305,17:",
            Texto.Normalizar(MemoriaCalculoPensao.ConferenciaDasFaixas(simulacao.Normal, valor => valor.ToString("C2", new System.Globalization.CultureInfo("pt-BR")))));

        // Desconto simplificado: 6.000,00 - 607,20 = 5.392,80, sem deduzir a pensão.
        // 29,83875 + 138,66 + 913,63 x 22,5% (205,56675) + 728,12 x 27,5% (200,233) = 574,2985.
        var simplificada = Assert.IsType<QuadroFaixasDto>(simulacao.Simplificada.QuadroIrrf);
        Assert.Equal(5_392.80m, simplificada.BaseCalculo);
        Assert.Equal(728.12m, simplificada.Faixas[^1].ParteDaBase);
        Assert.Equal(574.2985m, simplificada.ValorExato);
        Assert.Equal(574.30m, simplificada.Valor);
    }

    [Fact]
    public async Task Pensao_antes_de_marco_de_2020_nao_tem_quadro_do_inss()
    {
        var simulacao = await new SimularPensaoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularPensaoRequest(new DateOnly(2019, 6, 1), 6_000m, 6_000m, 0, new RegraPensao(BasePensao.ValorFixo, 0m, 1_000m), 0m), TestContext.Current.CancellationToken).Sucesso();
        Assert.Null(simulacao.QuadroInss);
        Assert.NotNull(simulacao.Normal.QuadroIrrf);
    }

    [Fact]
    public async Task Planilha_da_pensao_traz_as_faixas_em_celulas_numericas()
    {
        var simulacao = await PensaoFixaDeMilAsync();
        var entrada = new EntradaPensaoDto(new DateOnly(2026, 10, 1), 6_000m, 6_000m, 0, new RegraPensao(BasePensao.ValorFixo, 0m, 1_000m), 0m);
        var caminho = Path.Combine(Path.GetTempPath(), $"planilha-pensao-{Guid.NewGuid():N}.xlsx");
        try
        {
            await new ClosedXmlPlanilhaService().GerarPensaoAsync(simulacao, entrada, caminho, TestContext.Current.CancellationToken);
            using var pasta = new XLWorkbook(new MemoryStream(await File.ReadAllBytesAsync(caminho, TestContext.Current.CancellationToken)));
            var memoria = pasta.Worksheet("Memória de cálculo");
            var cabecalhos = memoria.RowsUsed().Where(linha => linha.Cell(1).GetString() == "Faixa" && linha.Cell(7).GetString() == "Valor considerado").ToList();
            // INSS, deduções legais e desconto simplificado.
            Assert.Equal(3, cabecalhos.Count);
            Assert.Equal(641.50d, cabecalhos[0].RowBelow(5).Cell(7).GetDouble(), 2);
            var faixa4Normal = cabecalhos[1].RowBelow(4);
            Assert.Equal(607.45d, faixa4Normal.Cell(4).GetDouble(), 2);
            Assert.Equal(136.67625d, faixa4Normal.Cell(6).GetDouble(), 5);
            Assert.Contains("R$", faixa4Normal.Cell(6).Style.NumberFormat.Format);
            Assert.Equal(305.18d, cabecalhos[1].RowBelow(6).Cell(7).GetDouble(), 2);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    private static async Task<SimulacaoPensaoDto> PensaoFixaDeMilAsync() =>
        await new SimularPensaoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularPensaoRequest(new DateOnly(2026, 10, 1), 6_000m, 6_000m, 0, new RegraPensao(BasePensao.ValorFixo, 0m, 1_000m), 0m), default).Sucesso();
}
