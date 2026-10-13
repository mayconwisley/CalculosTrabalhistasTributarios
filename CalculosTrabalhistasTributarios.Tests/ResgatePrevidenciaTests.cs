using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Resgate em 13/10/2026 de três aportes de 10.000,00: em 10/01/2016 (saldo 22.000,00, mais de 10 anos: 10%), em
/// 10/01/2021 (saldo 15.000,00, entre 4 e 6 anos: 25%) e em 10/01/2025 (saldo 11.000,00, até 2 anos: 35%).
/// </summary>
public class ResgatePrevidenciaTests
{
    private static readonly DateOnly Resgate = new(2026, 10, 13);

    private static AportePrevidencia[] Aportes() =>
    [
        new(new DateOnly(2025, 1, 10), 10000m, 11000m),   // fora de ordem: o resgate os ordena pela data
        new(new DateOnly(2016, 1, 10), 10000m, 22000m),
        new(new DateOnly(2021, 1, 10), 10000m, 15000m)
    ];

    [Fact]
    public void Pgbl_total_tributa_todo_o_valor_por_aporte()
    {
        var a = CalculadoraResgatePrevidencia.Calcular(new(TipoPlanoPrevidencia.Pgbl, RegimeTributacaoPrevidencia.Regressivo, Aportes(), Resgate)).Sucesso();

        // 22.000 x 10% + 15.000 x 25% + 11.000 x 35% = 2.200 + 3.750 + 3.850 = 9.800; progressivo: 48.000 x 15% = 7.200.
        Assert.Equal([10m, 25m, 35m], a.Parcelas.Select(p => p.Aliquota));
        Assert.Equal((48000m, 48000m, 9800m, 7200m, 38200m), (a.Resgatado, a.Base, a.ImpostoRegressivo, a.ImpostoProgressivo, a.Liquido));
    }

    [Fact]
    public void Vgbl_total_tributa_so_os_rendimentos()
    {
        var a = CalculadoraResgatePrevidencia.Calcular(new(TipoPlanoPrevidencia.Vgbl, RegimeTributacaoPrevidencia.Progressivo, Aportes(), Resgate)).Sucesso();

        // Rendimentos 12.000, 5.000 e 1.000: regressivo 1.200 + 1.250 + 350 = 2.800; progressivo 18.000 x 15% = 2.700, o retido.
        Assert.Equal((18000m, 2800m, 2700m, 2700m), (a.Base, a.ImpostoRegressivo, a.ImpostoProgressivo, a.Imposto));
    }

    [Fact]
    public void Resgate_parcial_consome_primeiro_os_aportes_mais_antigos()
    {
        var a = CalculadoraResgatePrevidencia.Calcular(new(TipoPlanoPrevidencia.Vgbl, RegimeTributacaoPrevidencia.Regressivo, Aportes(), Resgate, 30000m)).Sucesso();

        // 22.000 do aporte de 2016 (base 12.000, 10% = 1.200) e 8.000 do de 2021: base 8.000 x 5.000 ÷ 15.000 = 2.666,67, 25% = 666,67.
        Assert.Equal([22000m, 8000m], a.Parcelas.Select(p => p.Resgatado));
        Assert.Equal([12000m, 2666.67m], a.Parcelas.Select(p => p.Base));
        Assert.Equal((1866.67m, 2200m, 18000m), (a.ImpostoRegressivo, a.ImpostoProgressivo, a.SaldoRestante));
    }

    [Theory]
    [InlineData("2024-10-13", "2026-10-13", 35)]   // exatamente 2 anos: ainda 35%
    [InlineData("2024-10-13", "2026-10-14", 30)]
    [InlineData("2016-10-13", "2026-10-13", 15)]   // exatamente 10 anos: 15%
    [InlineData("2016-10-13", "2026-10-14", 10)]
    [InlineData("2024-02-29", "2026-02-28", 35)]   // 29/02 + 2 anos = 28/02
    [InlineData("2024-02-29", "2026-03-01", 30)]
    public void Faixas_da_tabela_regressiva(string aporte, string resgate, int aliquota)
    {
        Assert.Equal(aliquota, CalculadoraResgatePrevidencia.AliquotaRegressiva(Data(aporte), Data(resgate)));
    }

    private static DateOnly Data(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Saldo_estimado_pelos_meses_completos()
    {
        // 1.000 a 1% ao mês: 11 meses completos até 09/01/2026 (1.115,67) e 12 até 10/01/2026 (1.126,83).
        Assert.Equal(1115.67m, CalculadoraResgatePrevidencia.EstimarSaldo(1000m, new DateOnly(2025, 1, 10), new DateOnly(2026, 1, 9), 1m));
        Assert.Equal(1126.83m, CalculadoraResgatePrevidencia.EstimarSaldo(1000m, new DateOnly(2025, 1, 10), new DateOnly(2026, 1, 10), 1m));
    }

    [Theory]
    [InlineData(48000.01, "no máximo o saldo total (R$ 48.000,00)")]
    [InlineData(0, "deve ser positivo")]
    public void Resgate_parcial_invalido_e_recusado(double valor, string trecho)
    {
        var entrada = new EntradaResgatePrevidencia(TipoPlanoPrevidencia.Pgbl, RegimeTributacaoPrevidencia.Regressivo, Aportes(), Resgate, (decimal)valor);
        Assert.Contains(trecho, CalculadoraResgatePrevidencia.Calcular(entrada).Erro.Mensagem);
    }

    [Fact]
    public void Aporte_posterior_ao_resgate_e_recusado()
    {
        var entrada = new EntradaResgatePrevidencia(TipoPlanoPrevidencia.Pgbl, RegimeTributacaoPrevidencia.Regressivo, [new(new DateOnly(2026, 10, 14), 100m, 100m)], Resgate);
        Assert.Contains("posterior à data do resgate", CalculadoraResgatePrevidencia.Calcular(entrada).Erro.Mensagem);
    }

    [Fact]
    public async Task Demonstrativo_mostra_o_regime_escolhido_e_compara_o_outro()
    {
        var d = (await new SimularResgatePrevidenciaUseCase().ExecutarAsync(new SimularResgatePrevidenciaRequest(
            new(TipoPlanoPrevidencia.Pgbl, RegimeTributacaoPrevidencia.Regressivo, Aportes(), Resgate)), default)).Sucesso();

        Assert.Equal(38200m, d.Resultado);
        Assert.Equal("20,42% da base", Texto.Normalizar(d.Descontos[0].Referencia));   // 9.800 ÷ 48.000
        var comparativo = Assert.IsType<TabelaComparativaDto>(d.Comparativo);
        Assert.Equal(["R$ 38.200,00", "R$ 40.800,00"], comparativo.Linhas[1].Valores.Select(Texto.Normalizar));
        Assert.StartsWith("Aporte de 10/01/2016", d.Memoria[0].Formulas[0].Titulo);
    }
}
