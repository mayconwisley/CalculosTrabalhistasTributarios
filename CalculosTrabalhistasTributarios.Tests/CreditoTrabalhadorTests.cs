using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Xunit;
using CalculadoraCreditoTrabalhador = CalculosTrabalhistasTributarios.Domain.Trabalhista.CalculadoraCreditoTrabalhador;
using FormularioCreditoTrabalhador = CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras.CalculadoraCreditoTrabalhador;

namespace CalculosTrabalhistasTributarios.Tests;

public class CreditoTrabalhadorTests
{
    private static EntradaCreditoTrabalhador Entrada() => new(ObjetivoCreditoTrabalhador.ValorDesejado,
        1000m, 2, 10m, 0m, 0m, 0m, 3400m, 0m);

    private static EntradaCreditoTrabalhador Automatica() => Entrada() with
    {
        IofAutomatico = true, DataLiberacao = new(2026, 1, 1), PrimeiroVencimento = new(2026, 2, 1)
    };

    [Fact]
    public void Iof_financiado_resolve_inclusao_no_principal_sem_tributar_juros()
    {
        // Amortizações de um principal unitário a 10% em 2 meses: 10/21 e 11/21.
        // Dias: 31 e 59. q = 0,0038 + 0,000082 × (31×10 + 59×11)/21.
        // IOF financiado = 1.000 × q/(1-q) = 7,602...
        var a = CalculadoraCreditoTrabalhador.Calcular(Automatica()).Sucesso();
        Assert.Equal(7.60m, a.IofFinanciado);
        Assert.Equal(1007.60m, a.PrincipalFinanciado);
        Assert.Equal(1000m, a.CreditoLiquido);
        Assert.InRange(a.BaseDiasIof, 46013.733333m, 46013.733334m);
        Assert.Equal(a.PrincipalFinanciado + a.TotalJuros, a.TotalPago);
    }

    [Fact]
    public void Iof_pela_margem_e_custos_preservam_limite()
    {
        // Sem juros, amortiza 500 aos 31 dias e 500 aos 59: IOF = 3,80 + 3,69.
        var a = CalculadoraCreditoTrabalhador.Calcular(Automatica() with
        {
            Objetivo = ObjetivoCreditoTrabalhador.LimitePelaMargem, JurosMensais = 0m,
            MargemLivreOficial = 500m, OutrosCustosFinanciados = 10m, CustosDescontadosNaLiberacao = 20m
        }).Sucesso();
        Assert.Equal(7.49m, a.IofFinanciado);
        Assert.Equal(982.51m, a.ValorCredito);
        Assert.Equal(962.51m, a.CreditoLiquido);
        Assert.Equal(1000m, a.PrincipalFinanciado);
        Assert.Equal(500m, a.UltimaParcela);
        Assert.True(a.CabeNaMargem);
    }

    [Fact]
    public void Iof_limita_dias_a_365_mesmo_em_ano_bissexto()
    {
        var a = CalculadoraCreditoTrabalhador.Calcular(Automatica() with
        {
            NumeroParcelas = 1, JurosMensais = 0m,
            DataLiberacao = new(2027, 3, 1), PrimeiroVencimento = new(2028, 3, 1)
        }).Sucesso();
        // 366 dias reais; teto 365: q = 0,03373. 1.000*q/(1-q) = 34,9074...
        Assert.Equal(34.91m, a.IofFinanciado);
        Assert.Equal(a.PrincipalFinanciado * 365m, a.BaseDiasIof);
    }

    [Fact]
    public void Iof_ancora_vencimentos_no_dia_original_em_fevereiro_bissexto()
    {
        var a = CalculadoraCreditoTrabalhador.Calcular(Automatica() with
        {
            NumeroParcelas = 3, JurosMensais = 0m,
            DataLiberacao = new(2028, 1, 1), PrimeiroVencimento = new(2028, 1, 31)
        }).Sucesso();
        // 31/01, 29/02, 31/03: 30, 59, 90 dias, média 179/3; IOF = 8,76889...
        Assert.Equal(8.77m, a.IofFinanciado);
        Assert.InRange(a.BaseDiasIof / a.PrincipalFinanciado, 59.666666666m, 59.666666667m);
    }

    [Fact]
    public void Iof_valida_datas_e_modo_manual_independe_delas()
    {
        Assert.True(CalculadoraCreditoTrabalhador.Calcular(Automatica() with { DataLiberacao = null }).Falhou);
        Assert.True(CalculadoraCreditoTrabalhador.Calcular(Automatica() with { PrimeiroVencimento = new(2026, 1, 1) }).Falhou);
        Assert.True(CalculadoraCreditoTrabalhador.Calcular(Automatica() with { PrimeiroVencimento = new(2027, 1, 2) }).Falhou);
        Assert.True(CalculadoraCreditoTrabalhador.Calcular(Automatica() with { DataLiberacao = DateOnly.MaxValue }).Falhou);
        Assert.Equal(3.24m, CalculadoraCreditoTrabalhador.Calcular(Entrada() with { IofFinanciado = 3.24m }).Sucesso().IofFinanciado);
    }

    [Fact]
    public void Historico_antigo_preserva_iof_manual_e_novo_preserva_datas()
    {
        var calc = new FormularioCreditoTrabalhador(null!);
        Assert.False(calc.Campos.Single(c => c.Rotulo == "IOF financiado (R$)").Visivel);
        calc.ImportarCampos(new Dictionary<string, string> { ["IOF financiado (R$)"] = "3,24" });
        Assert.Equal("Informado pelo banco", calc.ExportarCampos()["Cálculo do IOF"]);
        Assert.True(calc.Campos.Single(c => c.Rotulo == "IOF financiado (R$)").Visivel);
        calc.ImportarCampos(new Dictionary<string, string>
        {
            ["Cálculo do IOF"] = "Automático (estimado)", ["Data de liberação"] = "07/10/2026",
            ["Primeiro vencimento"] = "20/11/2026"
        });
        var copia = new FormularioCreditoTrabalhador(null!);
        copia.ImportarCampos(calc.ExportarCampos());
        Assert.Equal(calc.ExportarCampos(), copia.ExportarCampos());
        Assert.True(copia.Campos.Single(c => c.Rotulo == "Primeiro vencimento").Visivel);
        Assert.False(copia.Campos.Single(c => c.Rotulo == "IOF financiado (R$)").Visivel);
    }

    [Fact]
    public void Price_referencia_e_margem_do_exemplo_oficial()
    {
        var a = CalculadoraCreditoTrabalhador.Calcular(Entrada()).Sucesso();
        // 1.000 × 0,10 × 1,21 / 0,21 = 576,190476; juros: 100 + 52,38.
        Assert.Equal(576.19m, a.Parcela);
        Assert.Equal(576.19m, a.UltimaParcela);
        Assert.Equal(1152.38m, a.TotalPago);
        Assert.Equal(152.38m, a.TotalJuros);
        Assert.Equal(1190m, a.MargemTotal); // MTE: 3.400 disponíveis × 35%.
        Assert.True(a.CabeNaMargem);
    }

    [Fact]
    public void Juros_zero_ajusta_ultima_parcela_sem_juros_negativos()
    {
        var a = CalculadoraCreditoTrabalhador.Calcular(Entrada() with { NumeroParcelas = 3, JurosMensais = 0m }).Sucesso();
        Assert.Equal(333.33m, a.Parcela);
        Assert.Equal(333.34m, a.UltimaParcela);
        Assert.Equal(1000m, a.TotalPago);
        Assert.Equal(0m, a.TotalJuros);
        Assert.Equal(0m, a.CustoEfetivoMensalEstimado);
    }

    [Fact]
    public void Custos_e_custo_efetivo_usam_credito_liquido_e_fluxo()
    {
        var a = CalculadoraCreditoTrabalhador.Calcular(Entrada() with
        { NumeroParcelas = 1, IofFinanciado = 50m, OutrosCustosFinanciados = 50m, CustosDescontadosNaLiberacao = 100m }).Sucesso();
        Assert.Equal(1100m, a.PrincipalFinanciado);
        Assert.Equal(900m, a.CreditoLiquido);
        Assert.Equal(1210m, a.TotalPago);
        Assert.Equal(310m, a.CustoTotal);
        Assert.InRange(a.CustoEfetivoMensalEstimado!.Value, 34.44444m, 34.44445m);
    }

    [Fact]
    public void Modo_margem_respeita_ultima_parcela_e_custos_fixos()
    {
        var entrada = Entrada() with { Objetivo = ObjetivoCreditoTrabalhador.LimitePelaMargem, MargemLivreOficial = 100m, IofFinanciado = 20m };
        var a = CalculadoraCreditoTrabalhador.Calcular(entrada).Sucesso();
        Assert.Equal(153.55m, a.ValorCredito);
        Assert.Equal(100m, a.Parcela);
        Assert.Equal(100m, a.UltimaParcela);
        Assert.True(a.CabeNaMargem);
        Assert.False(CalculadoraCreditoTrabalhador.Calcular(entrada with { Objetivo = ObjetivoCreditoTrabalhador.ValorDesejado, ValorDesejado = 153.56m }).Sucesso().CabeNaMargem);
    }

    [Fact]
    public void Margem_oficial_zero_e_ausencia_sao_estados_distintos()
    {
        Assert.Equal(1190m, CalculadoraCreditoTrabalhador.Calcular(Entrada()).Sucesso().MargemLivre);
        Assert.Equal(0m, CalculadoraCreditoTrabalhador.Calcular(Entrada() with { MargemLivreOficial = 0m }).Sucesso().MargemLivre);
        Assert.True(CalculadoraCreditoTrabalhador.Calcular(Entrada() with { MargemLivreOficial = 0m, Objetivo = ObjetivoCreditoTrabalhador.LimitePelaMargem }).Falhou);
    }

    [Fact]
    public void Validacoes_e_extremos_nao_causam_excecoes()
    {
        Assert.True(CalculadoraCreditoTrabalhador.Calcular(Entrada() with { ValorDesejado = decimal.MaxValue }).Falhou);
        Assert.True(CalculadoraCreditoTrabalhador.Calcular(Entrada() with { NumeroParcelas = 121 }).Falhou);
        Assert.True(CalculadoraCreditoTrabalhador.Calcular(Entrada() with { JurosMensais = -1m }).Falhou);
        Assert.True(CalculadoraCreditoTrabalhador.Calcular(Entrada() with { CustosDescontadosNaLiberacao = 1000m }).Falhou);
        Assert.True(CalculadoraCreditoTrabalhador.Calcular(Entrada() with { IofFinanciado = 0.001m }).Falhou);
    }

    [Fact]
    public async Task Caso_de_uso_apura_tributos_e_distingue_credito_de_salario()
    {
        var dto = await new SimularCreditoTrabalhadorUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new(new DateOnly(2026, 10, 1), 1000m, 0m, 0, 0m, 0m, Entrada()), default).Sucesso();
        Assert.Equal("R$ 323,75", dto.Destaques.Single(d => d.Rotulo == "Margem livre").Valor); // (1.000 - INSS 75 - IR zero) × 35%.
        Assert.Equal(1000m, dto.Resultado);
        Assert.Equal("Crédito líquido na liberação", dto.RotuloResultado);
        Assert.Contains(dto.Observacoes, texto => texto.StartsWith("A maior prestação"));
    }

    [Fact]
    public void Historico_preserva_modo_e_margem_opcional()
    {
        var calc = new FormularioCreditoTrabalhador(null!);
        calc.ImportarCampos(new Dictionary<string, string> { ["Objetivo"] = "Valor pela margem", ["Margem livre oficial (R$)"] = "0,00", ["Juros (% ao mês)"] = "1,5" });
        var copia = new FormularioCreditoTrabalhador(null!);
        copia.ImportarCampos(calc.ExportarCampos());
        Assert.Equal(calc.ExportarCampos(), copia.ExportarCampos());
        Assert.False(copia.Campos.Single(c => c.Rotulo == "Crédito desejado (R$)").Visivel);
    }
}
