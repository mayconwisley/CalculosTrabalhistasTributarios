using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class SalarioPeloLiquidoTests
{
    /// <summary>
    /// Alvos perto dos pontos em que o líquido cai de uma vez: as faixas do INSS com alíquota única (2019) e o IRRF que passa
    /// de R$ 10,00 e deixa de ter a retenção dispensada (2025 e 2026).
    /// </summary>
    [Theory]
    [InlineData("2019-06", 1611.67, 0, 1751.81)]
    [InlineData("2019-06", 2600.00, 0, 2919.15)]
    [InlineData("2019-06", 2546.47, 2, 2821.77)]
    [InlineData("2025-06", 2830.00, 0, 3094.77)]
    [InlineData("2025-06", 2834.60, 0, 3100.00)]
    [InlineData("2026-10", 4500.00, 0, 5001.74)]
    [InlineData("2026-10", 4510.00, 2, 5013.37)]
    [InlineData("2026-10", 6000.00, 0, 7934.81)]
    [InlineData("2024-06", 2560.00, 0, 2794.11)]
    public async Task Encontra_o_menor_bruto_com_o_liquido_exato(string mes, decimal liquido, int dependentes, decimal brutoEsperado)
    {
        var competencia = DateOnly.ParseExact(mes + "-01", "yyyy-MM-dd");
        var consulta = await Ambiente.ConsultaAsync();
        var demonstrativo = await new SimularSalarioPeloLiquidoUseCase(consulta).ExecutarAsync(new SimularSalarioPeloLiquidoRequest(competencia, liquido, dependentes), default).Sucesso();
        var bruto = demonstrativo.Proventos[0].Valor;
        Assert.Equal(brutoEsperado, bruto);

        // Nenhum bruto menor, até R$ 300,00 abaixo, pode dar o mesmo líquido.
        var simulador = new SimularImpostoUseCase(consulta);
        async Task<decimal> Liquido(decimal valor) => (await simulador.ExecutarAsync(new SimularImpostoRequest(competencia, valor, valor, dependentes), default).Sucesso()).SalarioLiquido;
        Assert.Equal(liquido, await Liquido(bruto));
        for (var valor = Math.Max(liquido, bruto - 300m); valor < bruto; valor += 0.01m)
            Assert.NotEqual(liquido, await Liquido(valor));
    }
}
