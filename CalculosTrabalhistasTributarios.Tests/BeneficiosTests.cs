using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Saque-aniversário do FGTS (Lei 8.036/1990, art. 20-D) e abono salarial do PIS/Pasep (EC 135/2024).</summary>
public class BeneficiosTests
{
    [Theory]
    [InlineData(400, 200.00)]
    [InlineData(500, 250.00)]
    [InlineData(800, 370.00)]
    [InlineData(3_000, 1_050.00)]
    [InlineData(8_000, 2_250.00)]
    [InlineData(12_000, 2_950.00)]
    [InlineData(18_000, 3_700.00)]
    [InlineData(30_000, 4_400.00)]
    public void Saque_aniversario_pela_faixa_do_saldo(decimal saldo, decimal saque) =>
        Assert.Equal(saque, SaqueAniversario.Calcular(saldo).Sucesso().Valor);

    [Fact]
    public async Task Saque_aniversario_mostra_o_periodo_e_o_saldo_restante()
    {
        var resultado = await new SimularSaqueAniversarioUseCase().ExecutarAsync(new SimularSaqueAniversarioRequest(8_000m, 11), default).Sucesso();

        Assert.Equal(2_250m, resultado.Resultado);
        Assert.Equal("Novembro a janeiro", resultado.Destaques[2].Valor);
    }

    private static async Task<Result<DemonstrativoDto>> AbonoAsync(int anoBase, int meses, decimal media, decimal limite = 0m, bool cadastrado = true) =>
        await new SimularAbonoSalarialUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularAbonoSalarialRequest(anoBase, meses, media, cadastrado, limite), default);

    [Theory]
    [InlineData(12, 1_621.00)]
    [InlineData(6, 810.50)]
    [InlineData(1, 135.08)]
    public async Task Abono_de_2026_e_proporcional_aos_meses(int meses, decimal valor)
    {
        var resultado = (await AbonoAsync(2024, meses, 2_500m)).Sucesso();

        Assert.Equal(valor, resultado.Resultado);
    }

    [Fact]
    public async Task Remuneracao_acima_do_limite_nao_da_direito()
    {
        var resultado = (await AbonoAsync(2024, 12, 2_800m)).Sucesso();

        Assert.Equal(0m, resultado.Resultado);
        Assert.Equal("Não", resultado.Destaques[1].Valor);
    }

    [Fact]
    public async Task Calendario_sem_limite_publicado_pede_o_limite()
    {
        Assert.Equal(TipoErro.Validacao, (await AbonoAsync(2025, 12, 2_000m)).Falha().Tipo);
        Assert.Equal(1_621m, (await AbonoAsync(2025, 12, 2_000m, limite: 2_880m)).Sucesso().Resultado);
    }
}
