using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class ImpostoRendaAnualTests
{
    private static Task<DemonstrativoDto> CalcularAsync(decimal tributaveis, decimal previdencia = 0m, decimal medicas = 0m, decimal impostoPago = 0m,
        decimal dividendos = 0m, decimal irrfDividendos = 0m, decimal aliquotaEmpresa = 0m) =>
        new SimularIrpfAnualUseCase().ExecutarAsync(new SimularIrpfAnualRequest(2026, tributaveis, previdencia, 0, medicas, 0m, 0m, 0m, impostoPago,
            dividendos, irrfDividendos, 0m, 0m, aliquotaEmpresa, AliquotaNominalEmpresa.Geral), default).Sucesso();

    private static decimal Informativo(DemonstrativoDto d, string inicio) => d.Informativos.Single(verba => verba.Descricao.StartsWith(inicio)).Valor;

    // Exemplos montados com a tabela e a redução publicadas pela Receita para 2026 (declaração de 2027).
    [Theory]
    [InlineData(60_000.00, 48_000.00, 2_694.15, 2_694.15)]
    [InlineData(70_000.00, 56_000.00, 4_495.34, 1_739.48)]
    [InlineData(80_000.00, 64_000.00, 6_695.34, 783.73)]
    [InlineData(88_200.00, 70_560.00, 8_499.34, 0.02)]
    [InlineData(100_000.00, 82_360.00, 11_744.34, 0.00)]
    public void Tabela_anual_e_reducao_no_simplificado(decimal rendimentos, decimal baseCalculo, decimal imposto, decimal reducao)
    {
        Assert.Equal(baseCalculo, rendimentos - ImpostoRendaAnual.DescontoSimplificado(rendimentos));
        Assert.Equal(imposto, ImpostoRendaAnual.ImpostoPelaTabela(baseCalculo));
        Assert.Equal(reducao, ImpostoRendaAnual.Reducao(rendimentos, imposto));
    }

    [Fact]
    public async Task Escolhe_o_modelo_de_menor_imposto()
    {
        // 70 mil com 25 mil de deduções: completa 2.020,09 - 1.739,48 = 280,61; simplificada 2.755,86.
        var resultado = await CalcularAsync(70_000m, previdencia: 7_000m, medicas: 18_000m, impostoPago: 1_000m);
        Assert.Equal("R$ 280,61", resultado.Destaques[0].Valor);
        Assert.Equal("Declaração completa", resultado.Destaques[0].Complemento);
        Assert.Equal("R$ 719,39", resultado.Destaques[1].Valor);
        Assert.Equal("A restituir", resultado.Destaques[1].Complemento);
    }

    [Fact]
    public void Aliquota_minima_cresce_ate_10_por_cento()
    {
        Assert.Equal(0m, ImpostoRendaAnual.AliquotaMinima(600_000m));
        Assert.Equal(2.5m, ImpostoRendaAnual.AliquotaMinima(750_000m));
        Assert.Equal(5m, ImpostoRendaAnual.AliquotaMinima(900_000m));
        Assert.Equal(7.5m, ImpostoRendaAnual.AliquotaMinima(1_050_000m));
        Assert.Equal(10m, ImpostoRendaAnual.AliquotaMinima(2_000_000m));
    }

    [Fact]
    public async Task So_dividendos_com_redutor_pela_aliquota_da_empresa()
    {
        // 1,2 milhão de dividendos: tributação mínima de 120 mil; empresa com 30% de alíquota efetiva: redutor de 1,2 mi x 6% = 72 mil.
        var resultado = await CalcularAsync(0m, dividendos: 1_200_000m, irrfDividendos: 120_000m, aliquotaEmpresa: 30m);
        Assert.Equal(48_000m, Informativo(resultado, "Tributação mínima devida"));
        Assert.Equal("R$ 72.000,00", resultado.Destaques[1].Valor);
        Assert.Equal("A restituir", resultado.Destaques[1].Complemento);
    }

    [Theory]
    [InlineData(34, 0.00, 108_000.00)]           // 34% + 9,51% passa de 34%: o redutor zera a tributação mínima
    [InlineData(20, 102_755.66, 5_244.34)]       // 20% + 9,51% não passa de 34%: sem redutor
    public async Task Salario_e_dividendos(decimal aliquotaEmpresa, decimal minimaDevida, decimal restituicao)
    {
        // 120 mil de salário (simplificado: 17.244,34 de IRPF) e 1,08 milhão de dividendos com 108 mil retidos.
        var resultado = await CalcularAsync(120_000m, impostoPago: 17_244.34m, dividendos: 1_080_000m, irrfDividendos: 108_000m, aliquotaEmpresa: aliquotaEmpresa);
        Assert.Equal(minimaDevida, Informativo(resultado, "Tributação mínima devida"));
        Assert.Equal(restituicao.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), resultado.Destaques[1].Valor);
    }

    [Theory]
    [InlineData(50_000.00, false, 0.00)]
    [InlineData(50_000.01, false, 5_000.00)]   // passou de 50 mil: 10% sobre o total
    [InlineData(10_000.00, true, 1_000.00)]    // no exterior, sobre qualquer valor
    public async Task Dividendos_retem_10_por_cento_do_total_acima_de_50_mil(decimal valor, bool exterior, decimal retencao)
    {
        var resultado = await new SimularDividendosUseCase().ExecutarAsync(new SimularDividendosRequest(new DateOnly(2026, 10, 1), valor, 0m, 0m, exterior), default).Sucesso();
        Assert.Equal(retencao, resultado.TotalDescontos);
    }

    [Fact]
    public async Task Dividendos_no_mesmo_mes_descontam_o_ja_retido()
    {
        // 30 mil no dia 5 sem retenção; mais 30 mil no dia 20: 6 mil sobre os 60 mil do mês.
        var resultado = await new SimularDividendosUseCase().ExecutarAsync(new SimularDividendosRequest(new DateOnly(2026, 10, 1), 60_000m, 0m, 0m, false), default).Sucesso();
        Assert.Equal(6_000m, resultado.TotalDescontos);
        var transicao = await new SimularDividendosUseCase().ExecutarAsync(new SimularDividendosRequest(new DateOnly(2026, 10, 1), 60_000m, 20_000m, 0m, false), default).Sucesso();
        Assert.Equal(0m, transicao.TotalDescontos);
    }
}
