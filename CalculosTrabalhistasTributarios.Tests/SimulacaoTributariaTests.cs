using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Tests.Referencia;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class SimulacaoTributariaTests
{
    public static TheoryData<string> Competencias => new(ModeloTributario.Competencias.Select(competencia => competencia.ToString("yyyy-MM")));

    [Theory]
    [MemberData(nameof(Competencias))]
    public async Task Bate_com_o_modelo_de_referencia_em_toda_a_faixa_de_salarios(string mes)
    {
        var competencia = DateOnly.ParseExact(mes + "-01", "yyyy-MM-dd");
        var simulador = new SimularImpostoUseCase(await Ambiente.ConsultaAsync());
        var diferencas = new List<string>();
        for (var bruto = 0m; bruto <= 12_000m; bruto += 7.31m)
            foreach (var dependentes in new[] { 0, 1, 3 })
            {
                var app = await simulador.ExecutarAsync(new SimularImpostoRequest(competencia, bruto, bruto, dependentes), default).Sucesso();
                var esperado = ModeloTributario.Calcular(competencia, bruto, dependentes);
                if (app.ValorInss != esperado.Inss || app.Normal.Imposto != esperado.ImpostoNormal || app.IrrfAplicado != esperado.IrrfRetido || app.SalarioLiquido != esperado.Liquido
                    || esperado.ImpostoSimplificado is { } simplificado && app.Simplificada.Imposto != simplificado)
                    diferencas.Add($"{bruto} com {dependentes} dependente(s): app {app.ValorInss}/{app.Normal.Imposto}/{app.IrrfAplicado}, esperado {esperado}");
            }
        Assert.Empty(diferencas.Take(10));
    }

    [Theory]
    [InlineData("2026-10", 8475.55, 988.07)]
    [InlineData("2026-10", 9000.00, 988.07)]
    [InlineData("2026-10", 5000.00, 501.50)]
    [InlineData("2026-10", 3000.00, 248.58)]
    [InlineData("2021-06", 6433.57, 751.97)]
    [InlineData("2019-06", 3000.00, 330.00)]
    public async Task Inss_trunca_cada_faixa_como_o_eSocial(string mes, decimal bruto, decimal inss)
    {
        var simulacao = await new SimularImpostoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularImpostoRequest(DateOnly.ParseExact(mes + "-01", "yyyy-MM-dd"), bruto, bruto, 0), default).Sucesso();
        Assert.Equal(inss, simulacao.ValorInss);
    }

    [Theory]
    [InlineData("2025-06", 3100.00, 4.80, true, 2834.60)]
    [InlineData("2026-10", 5010.00, 3.58, true, 4507.10)]
    [InlineData("2026-10", 5040.00, 14.32, false, 4518.58)]
    public async Task Irrf_de_ate_dez_reais_nao_e_retido(string mes, decimal bruto, decimal calculado, bool dispensado, decimal liquido)
    {
        var simulacao = await new SimularImpostoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularImpostoRequest(DateOnly.ParseExact(mes + "-01", "yyyy-MM-dd"), bruto, bruto, 0), default).Sucesso();
        Assert.Equal(calculado, simulacao.IrrfCalculado);
        Assert.Equal(dispensado, simulacao.RetencaoDispensada);
        Assert.Equal(dispensado ? 0m : calculado, simulacao.IrrfAplicado);
        Assert.Equal(liquido, simulacao.SalarioLiquido);
    }

    [Fact]
    public async Task Antes_de_maio_de_2023_nao_ha_desconto_simplificado()
    {
        var simulador = new SimularImpostoUseCase(await Ambiente.ConsultaAsync());
        var abril = await simulador.ExecutarAsync(new SimularImpostoRequest(new DateOnly(2023, 4, 1), 5000m, 5000m, 0), default).Sucesso();
        var maio = await simulador.ExecutarAsync(new SimularImpostoRequest(new DateOnly(2023, 5, 1), 5000m, 5000m, 0), default).Sucesso();
        Assert.Null(abril.DescontoSimplificado);
        Assert.False(abril.SimplificadaAplicada);
        Assert.Equal(528m, maio.DescontoSimplificado);
    }

    [Fact]
    public async Task Mensagem_de_vantagem_mostra_a_diferenca_em_reais()
    {
        var simulacao = await new SimularImpostoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularImpostoRequest(new DateOnly(2025, 6, 1), 3100m, 3100m, 0), default).Sucesso();
        Assert.Equal("O cálculo simplificado é mais vantajoso. Diferença: R$ 26,23.", Texto.Normalizar(simulacao.MensagemVantagem));
    }
}
