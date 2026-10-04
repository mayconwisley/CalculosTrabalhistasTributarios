using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Afastamento por doença e acidente (Lei 8.213/1991) e licenças-maternidade e paternidade (Lei 11.770/2008 e LC 229/2026).</summary>
public class AfastamentoTests
{
    private static async Task<DemonstrativoDto> CalcularAsync(TipoAfastamento tipo, string inicio, int dias, decimal remuneracao, bool empresaCidada = false) =>
        await new SimularAfastamentoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularAfastamentoRequest(tipo, DateOnly.Parse(inicio), dias, remuneracao, 0m, empresaCidada), default).Sucesso();

    private static decimal Valor(IEnumerable<VerbaDto> verbas, string inicio) => verbas.Single(verba => verba.Descricao.StartsWith(inicio, StringComparison.Ordinal)).Valor;

    [Fact]
    public async Task Doenca_divide_os_15_primeiros_dias_e_o_restante_com_o_inss()
    {
        // 3.000,00 ÷ 30 x 15 = 1.500,00 pela empresa; 91% de 3.000,00 = 2.730,00 ÷ 30 x 15 = 1.365,00 pelo INSS.
        var resultado = await CalcularAsync(TipoAfastamento.Doenca, "2026-10-01", 30, 3_000m);

        Assert.Equal(1_500.00m, Valor(resultado.Proventos, "15 primeiros dias"));
        Assert.Equal(1_365.00m, Valor(resultado.Proventos, "Auxílio por incapacidade temporária"));
        Assert.Equal(120.00m, Valor(resultado.Informativos, "FGTS"));
        Assert.Equal("31/10/2026", resultado.Destaques[0].Valor);
    }

    [Fact]
    public async Task Auxilio_nao_fica_abaixo_do_salario_minimo()
    {
        // 91% de 1.000,00 = 910,00, abaixo do mínimo de 1.621,00.
        var resultado = await CalcularAsync(TipoAfastamento.Doenca, "2026-10-01", 30, 1_000m);

        Assert.Equal(1_621m / 30m * 15m, Valor(resultado.Proventos, "Auxílio"), 2);
    }

    [Fact]
    public async Task Acidente_mantem_o_fgts_e_garante_12_meses_de_estabilidade()
    {
        var resultado = await CalcularAsync(TipoAfastamento.AcidenteDeTrabalho, "2026-10-01", 30, 3_000m);

        Assert.Equal(240.00m, Valor(resultado.Informativos, "FGTS"));
        Assert.Equal("30/10/2027", resultado.Destaques[3].Valor);
    }

    [Fact]
    public async Task Maternidade_da_empresa_cidada_tem_180_dias()
    {
        var resultado = await CalcularAsync(TipoAfastamento.Maternidade, "2026-10-01", 0, 3_000m, empresaCidada: true);

        Assert.Equal(12_000.00m, Valor(resultado.Proventos, "Salário-maternidade"));
        Assert.Equal(6_000.00m, Valor(resultado.Proventos, "Prorrogação"));
        Assert.Contains("180 dias", resultado.Referencia);
    }

    [Theory]
    [InlineData("2026-10-01", false, 5)]
    [InlineData("2027-03-01", false, 10)]
    [InlineData("2029-03-01", false, 20)]
    [InlineData("2026-10-01", true, 20)]
    public async Task Paternidade_segue_a_transicao_da_lc_229(string inicio, bool empresaCidada, int dias)
    {
        var resultado = await CalcularAsync(TipoAfastamento.Paternidade, inicio, 0, 3_000m, empresaCidada);

        Assert.Equal(100m * dias, Valor(resultado.Proventos, "Licença-paternidade"));
    }
}
