using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Complemento do 13º por remuneração variável (Decreto 57.155/1965, art. 2º). Valores calculados à mão: salário de
/// 3.000, variáveis de 11.000 até novembro (média paga 1.000) e 2.200 em dezembro (média final 13.200 ÷ 12 = 1.100).
/// 13º pago 4.000, revisado 4.100. INSS de 2025 com truncamento por faixa: 373,40 → 385,40 (+ 12,00).
/// IRRF de 12/2025 pelo desconto simplificado: 114,76 → 129,76 (+ 15,00).
/// </summary>
public class ComplementoDecimoTerceiroTests
{
    private static readonly DateOnly Dezembro = new(2025, 12, 1);

    private static async Task<DemonstrativoDto> SimularAsync(DateOnly pagamento, TributacaoComplementoDecimoTerceiro tributacao, decimal mediaPaga = 1_000m) =>
        (await new SimularComplementoDecimoTerceiroUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularComplementoDecimoTerceiroRequest(
            Dezembro, pagamento, 3_000m, mediaPaga, 11_000m, 2_200m, 12, 0, tributacao), default)).Sucesso();

    [Fact]
    public void Media_final_inclui_dezembro_e_divide_pelos_avos()
    {
        var c = CalculadoraComplementoDecimoTerceiro.Calcular(3_000m, 1_000m, 11_000m, 2_200m, 12).Sucesso();
        Assert.Equal((1_100m, 4_000m, 4_100m, 100m), (c.MediaFinal, c.IntegralPago, c.IntegralRevisado, c.Diferenca));

        // Seis avos: 6.600 ÷ 6 = 1.100; 13º revisado (3.000 + 1.100) ÷ 12 × 6 = 2.050.
        Assert.Equal(2_050m, CalculadoraComplementoDecimoTerceiro.Calcular(3_000m, 1_000m, 5_500m, 1_100m, 6).Sucesso().IntegralRevisado);
        Assert.True(CalculadoraComplementoDecimoTerceiro.Calcular(3_000m, 1_000m, 11_000m, 2_200m, 13).Falhou);
    }

    [Fact]
    public async Task Pago_em_janeiro_como_rra_de_um_mes_tem_base_isenta()
    {
        // RRA de 1 mês em 01/2026: base 100 - 12 = 88, na faixa isenta.
        var d = await SimularAsync(new DateOnly(2026, 1, 1), TributacaoComplementoDecimoTerceiro.Rra);

        Assert.Equal(100m, d.TotalProventos);
        Assert.Equal([("INSS sobre o complemento", 12m)], d.Descontos.Select(verba => (verba.Descricao, verba.Valor)));
        Assert.Equal(88m, d.Resultado);
        Assert.Contains(d.Memoria, grupo => grupo.Titulo == "IRRF do complemento (RRA de 1 mês)" && grupo.Formulas.Any(f => f.Formula.Contains("R$ 15,00")));
    }

    [Fact]
    public async Task Recalculo_do_13o_cobra_a_diferenca_de_irrf_e_vale_sempre_no_mesmo_ano()
    {
        var recalculo = await SimularAsync(new DateOnly(2026, 1, 1), TributacaoComplementoDecimoTerceiro.Recalculo);
        Assert.Equal(73m, recalculo.Resultado); // 100 - 12 - 15.

        var mesmoAno = await SimularAsync(Dezembro, TributacaoComplementoDecimoTerceiro.Rra);
        Assert.Equal(73m, mesmoAno.Resultado);
        Assert.Equal(["R$ 114,76", "R$ 129,76", "+R$ 15,00"], mesmoAno.Comparativo!.Linhas[^1].Valores.Select(valor => valor.Replace(" ", " ")));
    }

    [Fact]
    public async Task Media_final_menor_gera_compensacao()
    {
        var d = await SimularAsync(new DateOnly(2026, 1, 1), TributacaoComplementoDecimoTerceiro.Rra, mediaPaga: 1_200m);

        Assert.Empty(d.Proventos);
        Assert.Equal(100m, d.Descontos.Single().Valor); // (3.000 + 1.200) ÷ 12 × 12 - 4.100.
        Assert.Contains(d.Informativos, verba => verba.Descricao == "INSS do 13º descontado a maior");
    }

    [Fact]
    public async Task Calculadora_de_13o_alterna_para_o_complemento_e_oculta_campos_da_2a_parcela()
    {
        var consulta = await Ambiente.ConsultaAsync();
        var calculadora = new CalculadoraDecimoTerceiro(new SimularDecimoTerceiroUseCase(consulta), new SimularComplementoDecimoTerceiroUseCase(consulta));
        calculadora.ImportarCampos(new Dictionary<string, string>
        {
            ["Cálculo"] = "Complemento das médias (até 10/01)", ["Competência do pagamento"] = "12/2025", ["Salário"] = "3.000,00",
            ["Médias de variáveis"] = "1.000,00", ["Variáveis de janeiro a novembro"] = "11.000,00", ["Variáveis de dezembro"] = "2.200,00",
            ["Pagamento do complemento"] = "01/2026", ["Pensão alimentícia"] = "% do bruto"
        });

        Assert.False(calculadora.Campos.Single(campo => campo.Rotulo == "1ª parcela (adiantamento)").Visivel);
        Assert.False(calculadora.Campos.Single(campo => campo.Rotulo == "Percentual da pensão").Visivel);
        Assert.Equal(88m, (await calculadora.CalcularAsync(default)).Sucesso().Resultado);

        calculadora.ImportarCampos(new Dictionary<string, string> { ["Cálculo"] = "1ª e 2ª parcelas" });
        Assert.True(calculadora.Campos.Single(campo => campo.Rotulo == "Percentual da pensão").Visivel);
        Assert.False(calculadora.Campos.Single(campo => campo.Rotulo == "Variáveis de dezembro").Visivel);
    }
}
