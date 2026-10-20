using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class ImpostoRendaAnualTests
{
    private static Task<DemonstrativoDto> CalcularAsync(decimal tributaveis, decimal previdencia = 0m, decimal medicas = 0m, decimal impostoPago = 0m,
        decimal dividendos = 0m, decimal irrfDividendos = 0m, decimal aliquotaEmpresa = 0m) =>
        new SimularIrpfAnualUseCase().ExecutarAsync(new SimularIrpfAnualRequest(2026, tributaveis, previdencia, 0, medicas, 0m, 0m, 0m, impostoPago,
            dividendos, irrfDividendos, 0m, 0m, aliquotaEmpresa, AliquotaNominalEmpresa.Geral), default).Sucesso();

    private static Task<Result<DemonstrativoDto>> AposentadoAsync(decimal tributaveis, decimal aposentadoria, int meses, decimal outros = 0m) =>
        new SimularIrpfAnualUseCase().ExecutarAsync(new SimularIrpfAnualRequest(2026, tributaveis, 0m, 0, 0m, 0m, 0m, 0m, 0m, 0m, 0m, outros, 0m, 0m, AliquotaNominalEmpresa.Geral,
            aposentadoria, meses), default);

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
        var resultado = await new SimularDividendosUseCase().ExecutarAsync(new SimularDividendosRequest(new DateOnly(2026, 10, 1), valor, 0m, 0m, exterior), TestContext.Current.CancellationToken).Sucesso();
        Assert.Equal(retencao, resultado.TotalDescontos);
    }

    [Fact]
    public async Task Dividendos_no_mesmo_mes_descontam_o_ja_retido()
    {
        // 30 mil no dia 5 sem retenção; mais 30 mil no dia 20: 6 mil sobre os 60 mil do mês.
        var resultado = await new SimularDividendosUseCase().ExecutarAsync(new SimularDividendosRequest(new DateOnly(2026, 10, 1), 60_000m, 0m, 0m, false), TestContext.Current.CancellationToken).Sucesso();
        Assert.Equal(6_000m, resultado.TotalDescontos);
        var transicao = await new SimularDividendosUseCase().ExecutarAsync(new SimularDividendosRequest(new DateOnly(2026, 10, 1), 60_000m, 20_000m, 0m, false), TestContext.Current.CancellationToken).Sucesso();
        Assert.Equal(0m, transicao.TotalDescontos);
    }

    // 120 mil de aposentadoria com 65 anos o ano todo: 12 x R$ 1.903,98 = R$ 22.847,76 isentos e R$ 97.152,24 tributáveis,
    // sem redução anual (acima de R$ 88.200,00). Simplificada: desconto no limite de R$ 17.640,00, base de R$ 79.512,24,
    // x 27,5% - R$ 10.904,66 = R$ 10.961,21; completa sem deduções: R$ 97.152,24 x 27,5% - R$ 10.904,66 = R$ 15.812,21.
    // No ano dos 65, com 6 meses: R$ 60.000,00 + R$ 60.000,00 - R$ 11.423,88 = R$ 108.576,12 tributáveis; simplificada
    // com base de R$ 90.936,12: R$ 14.102,77. Sem a parcela, os R$ 120.000,00 dão R$ 17.244,34.
    [Theory]
    [InlineData(0, 120_000, 12, "R$ 10.961,21", 22_847.76, "R$ 97.152,24")]
    [InlineData(60_000, 60_000, 6, "R$ 14.102,77", 11_423.88, "R$ 108.576,12")]
    public async Task Aposentadoria_com_65_anos_exclui_a_parcela_isenta_do_ajuste(decimal tributaveis, decimal aposentadoria, int meses, string devido, decimal isenta, string tributaveisAjuste)
    {
        var resultado = (await AposentadoAsync(tributaveis, aposentadoria, meses)).Sucesso();
        var semParcela = await CalcularAsync(tributaveis + aposentadoria);

        Assert.Equal(devido, resultado.Destaques[0].Valor);
        Assert.Equal("R$ 17.244,34", semParcela.Destaques[0].Valor);
        Assert.Equal(isenta, Informativo(resultado, "Parcela isenta da aposentadoria"));
        Assert.Equal(tributaveisAjuste, resultado.Comparativo!.Linhas[0].Valores[0]);
    }

    [Fact]
    public async Task Parcela_isenta_limita_se_a_aposentadoria_e_continua_na_tributacao_minima()
    {
        // R$ 10.000,00 cabem no limite de 12 meses: tudo isento, e os R$ 50.000,00 tributáveis ficam abaixo de 60 mil.
        var pequena = (await AposentadoAsync(50_000m, 10_000m, 12)).Sucesso();
        Assert.Equal(10_000m, Informativo(pequena, "Parcela isenta da aposentadoria"));
        Assert.Equal("R$ 50.000,00", pequena.Comparativo!.Linhas[0].Valores[0]);

        // A parcela isenta não está nas exclusões do art. 16-A, § 1º: os 700 mil inteiros contam na tributação mínima.
        var alta = (await AposentadoAsync(0m, 700_000m, 12)).Sucesso();
        Assert.Equal(700_000m, Informativo(alta, "Rendimentos para a tributação mínima"));
        Assert.Equal("R$ 677.152,24", alta.Comparativo!.Linhas[0].Valores[0]);
    }

    [Theory]
    [InlineData(1_000, 13, "Meses com 65 anos ou mais:")]
    [InlineData(1_000, 0, "Meses com 65 anos ou mais:")]
    public async Task Aposentadoria_com_65_anos_valida_os_meses(decimal aposentadoria, int meses, string campo)
    {
        var resultado = await AposentadoAsync(0m, aposentadoria, meses);
        Assert.StartsWith(campo, resultado.Erro.Mensagem, StringComparison.Ordinal);
    }
}
