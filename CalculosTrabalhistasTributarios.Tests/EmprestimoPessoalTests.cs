using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Financeiro;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Xunit;
using Calculadora = CalculosTrabalhistasTributarios.Domain.Financeiro.CalculadoraEmprestimoPessoal;
using Formulario = CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras.CalculadoraEmprestimoPessoal;

namespace CalculosTrabalhistasTributarios.Tests;

public class EmprestimoPessoalTests
{
    private static EntradaEmprestimoPessoal Entrada() => new(1000m, 2, 10m, new(2026, 1, 1), false);

    [Fact]
    public void Price_mensal_confere_com_referencia_independente()
    {
        // 1000 × 0,10 × 1,21 / 0,21 = 576,190476; juros 100 + 52,38.
        var a = Calculadora.Calcular(Entrada()).Sucesso();
        Assert.Equal(576.19m, a.Parcela);
        Assert.Equal(576.19m, a.UltimaParcela);
        Assert.Equal(1152.38m, a.TotalPago);
        Assert.Equal(152.38m, a.TotalJuros);
        Assert.Equal(1000m, a.CreditoLiquido);
    }

    [Fact]
    public void Juros_zero_ajusta_ultima_e_custo_efetivo_zero()
    {
        var a = Calculadora.Calcular(Entrada() with { JurosMensais = 0m, NumeroParcelas = 3 }).Sucesso();
        Assert.Equal(333.33m, a.Parcela);
        Assert.Equal(333.34m, a.UltimaParcela);
        Assert.Equal(1000m, a.TotalPago);
        Assert.Equal(0m, a.CustoEfetivoMensal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Iof_automatico_financiado_ou_retido(bool financiado)
    {
        // Pesos das amortizações: 10/21 e 11/21; dias 31 e 59.
        // q = 0,0038 + 0,000082 × (31×10/21 + 59×11/21) = 0,007544666...
        var a = Calculadora.Calcular(Entrada() with { IofAutomatico = true, FinanciarIof = financiado }).Sucesso();
        Assert.Equal(financiado ? 1007.60m : 1000m, a.PrincipalFinanciado);
        Assert.Equal(financiado ? 1000m : 992.46m, a.CreditoLiquido);
        Assert.Equal(financiado ? 7.60m : 7.54m, a.Iof);
    }

    [Fact]
    public void Seguro_custos_e_iof_manual_nao_sao_contados_duas_vezes()
    {
        var e = Entrada() with { NumeroParcelas = 1, IofInformado = 20m, SeguroFinanciado = 50m, OutrosCustosFinanciados = 30m, CustosNaLiberacao = 100m };
        var a = Calculadora.Calcular(e).Sucesso();
        Assert.Equal(1100m, a.PrincipalFinanciado);
        Assert.Equal(900m, a.CreditoLiquido);
        Assert.Equal(1210m, a.TotalPago);
        Assert.Equal(310m, a.CustoTotal);
        Assert.InRange(a.CustoEfetivoMensal.GetValueOrDefault(), 34.44444m, 34.44445m);
        var b = Calculadora.Calcular(e with { FinanciarIof = false }).Sucesso();
        Assert.Equal(1080m, b.PrincipalFinanciado);
        Assert.Equal(880m, b.CreditoLiquido);
    }

    [Fact]
    public void Calendario_ancora_no_primeiro_vencimento_em_ano_bissexto()
    {
        var a = Calculadora.Calcular(Entrada() with { DataLiberacao = new(2028, 1, 31), IofAutomatico = true, JurosMensais = 0m }).Sucesso();
        Assert.Equal(new DateOnly(2028, 2, 29), a.PrimeiroVencimento);
        Assert.Equal(new DateOnly(2028, 3, 29), a.UltimoVencimento);
        // Dias 29 e 58; q = 0,007367; IOF financiado 1000q/(1-q).
        Assert.Equal(7.42m, a.Iof);
    }

    [Fact]
    public void Iof_limita_dias_de_cada_amortizacao_a_365()
    {
        var a = Calculadora.Calcular(new(1200m, 24, 0m, new(2025, 1, 1), true, FinanciarIof: false)).Sucesso();
        // Amortizações de 50; primeiros 11 vencimentos: soma 1998 dias.
        // Do 12º ao 24º: 13 × 365. Base = 50 × 6743 = 337150.
        Assert.Equal(337150m, decimal.Round(a.BaseDiasIof, 6));
        Assert.Equal(32.21m, a.Iof);
    }

    [Fact]
    public void Rejeita_limites_sem_excecao_e_aceita_maximos()
    {
        var e = Entrada();
        foreach (var invalida in new[] { e with { ValorSolicitado = 0m }, e with { ValorSolicitado = decimal.MaxValue },
            e with { NumeroParcelas = 121 }, e with { JurosMensais = 20.000001m },
            e with { JurosMensais = 0.0000001m }, e with { SeguroFinanciado = -1m },
            e with { IofInformado = 0.001m }, e with { CustosNaLiberacao = 1000m },
            e with { DataLiberacao = DateOnly.MaxValue }, e with { DataLiberacao = new(2024, 12, 31) },
            e with { ValorSolicitado = 0.01m, NumeroParcelas = 120 } })
            Assert.True(Calculadora.Calcular(invalida).Falhou);
        Assert.True(Calculadora.Calcular(e with { ValorSolicitado = 10_000_000m, NumeroParcelas = 120, JurosMensais = 20m, DataLiberacao = new(2100, 12, 31), IofAutomatico = true }).Sucesso);
    }

    [Theory]
    [InlineData("Automático (estimado)", "Financiado")]
    [InlineData("Informado pelo banco", "Descontado do crédito")]
    public async Task Historico_reabre_e_recalcula_com_os_mesmos_valores(string modo, string cobranca)
    {
        var original = new Formulario(new SimularEmprestimoPessoalUseCase());
        original.ImportarCampos(new Dictionary<string, string> {
            ["Valor solicitado (R$)"] = "1.000,00", ["Parcelas (meses)"] = "2", ["Juros (% ao mês)"] = "10",
            ["Data de liberação"] = "01/01/2026", ["Cálculo do IOF"] = modo, ["Cobrança do IOF"] = cobranca,
            ["IOF informado (R$)"] = "20,00", ["Seguro financiado (R$)"] = "50,00", ["Outros financiados (R$)"] = "30,00", ["Custos na liberação (R$)"] = "100,00" });
        var dados = DadosFormulario.DeJson(new DadosFormulario(original.ExportarCampos()).ParaJson());
        var copia = new Formulario(new SimularEmprestimoPessoalUseCase());
        copia.ImportarCampos(dados.Campos);
        Assert.Equal(original.ExportarCampos(), copia.ExportarCampos());
        Assert.Equal(modo == "Informado pelo banco", copia.Campos.Single(c => c.Rotulo == "IOF informado (R$)").Visivel);
        var a = await original.CalcularAsync(default).Sucesso();
        var b = await copia.CalcularAsync(default).Sucesso();
        Assert.Equal(a.Resultado, b.Resultado);
        Assert.Equal(a.Informativos, b.Informativos);
        Assert.Equal(a.Destaques, b.Destaques);
    }
}
