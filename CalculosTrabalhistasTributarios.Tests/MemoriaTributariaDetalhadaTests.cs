using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Memória do INSS e do IRRF faixa por faixa nas calculadoras de folha. Valores calculados à mão com as tabelas de 2026
/// (INSS até 1.621,00 / 2.902,84 / 4.354,27 / 8.475,55) e de 2025 (INSS até 1.518,00 / 2.793,88 / 4.190,83 / 8.157,41).
/// O IRRF usa as faixas até 2.428,80 / 2.826,65 / 3.751,05 / 4.664,68, vigentes desde 05/2025; a redução mensal só existe em 2026.
/// </summary>
public class MemoriaTributariaDetalhadaTests
{
    private static string Formula(DemonstrativoDto demonstrativo, string grupo, string titulo) =>
        demonstrativo.Memoria.Single(item => item.Titulo == grupo).Formulas.Single(formula => formula.Titulo == titulo).Formula.Replace(' ', ' ');

    internal static async Task<DemonstrativoDto> HoleriteDeSeisMilAsync() =>
        await new SimularHoleriteUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularHoleriteRequest(
            new DateOnly(2026, 10, 1), 6000m, GrauInsalubridade.Nenhum, false, 0m, 220m, 0m, 50m, 0m, 100m, 0m, 20m, 0, 0, 0, 0m, 0, 0, null, 0m, 0m, 0m), default).Sucesso();

    [Fact]
    public async Task Holerite_mostra_as_quatro_faixas_do_inss_e_a_diferenca_de_centavo_do_irrf()
    {
        var resultado = await HoleriteDeSeisMilAsync();

        // INSS: 121,57 + 115,36 + 174,17 + (6.000,00 - 4.354,27) x 14% = 230,4022 → 230,40; total 641,50.
        Assert.Equal("Parte acima de R$ 4.354,27: R$ 6.000,00 (base) - R$ 4.354,27 = R$ 1.645,73; R$ 1.645,73 x 14% = R$ 230,4022, truncado em R$ 230,40",
            Formula(resultado, "INSS", "Faixa 4 (14%)"));
        Assert.Equal("R$ 121,57 + R$ 115,36 + R$ 174,17 + R$ 230,40 = R$ 641,50", Formula(resultado, "INSS", "Total"));

        // IRRF com deduções legais: base 5.358,50; faixas 29,83875 + 138,66 + 205,56675 + 190,8005 = 564,866 → 564,87,
        // enquanto a parcela a deduzir dá 5.358,50 x 27,5% - 908,73 = 564,86. Redução: 978,62 - 0,133145 x 6.000 = 179,75.
        const string faixas = "IRRF por faixas (deduções legais)";
        Assert.Equal("Parte acima de R$ 4.664,68: R$ 5.358,50 (base) - R$ 4.664,68 = R$ 693,82; R$ 693,82 x 27,5% = R$ 190,8005",
            Formula(resultado, faixas, "Faixa 5 (27,5%)"));
        Assert.StartsWith("R$ 0,00 + R$ 29,83875 + R$ 138,66 + R$ 205,56675 + R$ 190,8005 = R$ 564,866, arredondada para R$ 564,87",
            Formula(resultado, faixas, "Soma das faixas"));
        Assert.Contains("R$ 5.358,50 x 27,5% - R$ 908,73 = R$ 564,86", Formula(resultado, faixas, "Pela parcela a deduzir"));
        Assert.Contains("R$ 179,75", Formula(resultado, faixas, "Redução mensal"));
        Assert.StartsWith("R$ 564,86 - R$ 179,75 (redução) = R$ 385,11", Formula(resultado, faixas, "IRRF da modalidade"));
    }

    [Fact]
    public async Task Quadro_de_faixas_repete_a_apuracao_em_numeros()
    {
        var resultado = await HoleriteDeSeisMilAsync();

        var inss = resultado.Memoria.Single(grupo => grupo.Titulo == "INSS").Quadro;
        Assert.NotNull(inss);
        Assert.Equal([1_621.00m, 1_281.84m, 1_451.43m, 1_645.73m], inss.Faixas.Select(faixa => faixa.ParteDaBase));
        Assert.Equal([121.575m, 115.3656m, 174.1716m, 230.4022m], inss.Faixas.Select(faixa => faixa.ValorExato));
        Assert.Equal([121.57m, 115.36m, 174.17m, 230.40m], inss.Faixas.Select(faixa => faixa.Valor));
        Assert.Equal((6_000m, 641.5144m, 641.50m), (inss.BaseCalculo, inss.ValorExato, inss.Valor));
        Assert.Equal((2_902.85m, 4_354.27m), (inss.Faixas[2].De, inss.Faixas[2].Ate));

        // IRRF: as cinco faixas, a última sem limite; a soma exata 564,866 arredonda para 564,87.
        var irrf = resultado.Memoria.Single(grupo => grupo.Titulo == "IRRF por faixas (deduções legais)").Quadro;
        Assert.NotNull(irrf);
        Assert.Equal([2_428.80m, 397.85m, 924.40m, 913.63m, 693.82m], irrf.Faixas.Select(faixa => faixa.ParteDaBase));
        Assert.Equal([0m, 7.5m, 15m, 22.5m, 27.5m], irrf.Faixas.Select(faixa => faixa.Aliquota));
        Assert.Equal((4_664.69m, (decimal?)null), (irrf.Faixas[4].De, irrf.Faixas[4].Ate));
        Assert.Equal((5_358.50m, 564.866m, 564.87m), (irrf.BaseCalculo, irrf.ValorExato, irrf.Valor));

        // Grupos sem faixas não trazem quadro.
        Assert.Null(resultado.Memoria.Single(grupo => grupo.Titulo == "IRRF").Quadro);
    }

    [Fact]
    public async Task Tela_mostra_o_quadro_com_os_valores_formatados()
    {
        var resultado = await HoleriteDeSeisMilAsync();
        var quadro = resultado.Memoria.Single(grupo => grupo.Titulo == "IRRF por faixas (deduções legais)").Quadro;
        Assert.NotNull(quadro);

        var tela = QuadroFaixasViewModel.De(quadro);
        Assert.Equal(("5", "R$ 4.664,69", "Sem limite", "R$ 693,82", "27,5%", "R$ 190,8005", "R$ 190,8005"),
            Normalizar(tela.Linhas[4]));
        Assert.Equal(("R$ 5.358,50", "R$ 564,866", "R$ 564,87"), (Texto.Normalizar(tela.BaseCalculo), Texto.Normalizar(tela.ValorExato), Texto.Normalizar(tela.Valor)));

        static (string, string, string, string, string, string, string) Normalizar(LinhaQuadroFaixasViewModel linha) =>
            (linha.Faixa, Texto.Normalizar(linha.De), linha.Ate, Texto.Normalizar(linha.ParteDaBase), linha.Aliquota, Texto.Normalizar(linha.ValorExato), Texto.Normalizar(linha.Valor));
    }

    [Fact]
    public async Task Ferias_mostram_as_faixas_do_desconto_simplificado()
    {
        var resultado = await new SimularFeriasUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularFeriasRequest(new DateOnly(2025, 10, 1), 3000m, 0m, 0, 30, false, false, 0), default).Sucesso();

        // Em 2025, sem a redução mensal. Férias + 1/3 = 4.000,00; INSS da faixa 3: (4.000,00 - 2.793,88) x 12% = 144,7344 → 144,73.
        var inss = resultado.Memoria.Single(grupo => grupo.Titulo.StartsWith("INSS") && grupo.Formulas.Any(formula => formula.Titulo == "Faixa 3 (12%)"));
        Assert.Equal("Parte acima de R$ 2.793,88: R$ 4.000,00 (base) - R$ 2.793,88 = R$ 1.206,12; R$ 1.206,12 x 12% = R$ 144,7344, truncado em R$ 144,73",
            inss.Formulas.Single(formula => formula.Titulo == "Faixa 3 (12%)").Formula.Replace(' ', ' '));

        // O desconto simplificado (607,20) supera o INSS (373,40): base 3.392,80; faixa 3 = 566,15 x 15% = 84,9225.
        const string faixas = "IRRF sobre as férias por faixas (desconto simplificado)";
        Assert.Equal("Parte acima de R$ 2.826,65: R$ 3.392,80 (base) - R$ 2.826,65 = R$ 566,15; R$ 566,15 x 15% = R$ 84,9225",
            Formula(resultado, faixas, "Faixa 3 (15%)"));
        Assert.StartsWith("R$ 0,00 + R$ 29,83875 + R$ 84,9225 = R$ 114,76125, arredondada para R$ 114,76", Formula(resultado, faixas, "Soma das faixas"));
    }

    [Fact]
    public async Task Decimo_terceiro_mostra_as_faixas_do_inss_e_do_irrf()
    {
        var resultado = await new SimularDecimoTerceiroUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularDecimoTerceiroRequest(new DateOnly(2025, 12, 1), 3100m, 0m, 12, 0, AdiantamentoDecimoTerceiro.CinquentaPorCento, 0m), default).Sucesso();

        // Em 2025, sem a redução mensal. INSS: 113,85 + 114,82 + (3.100,00 - 2.793,88) x 12% = 36,7344 → 36,73; total 265,40.
        Assert.Equal("R$ 113,85 + R$ 114,82 + R$ 36,73 = R$ 265,40", Formula(resultado, "INSS sobre o 13º", "Total"));

        // Desconto simplificado: base 3.100,00 - 607,20 = 2.492,80; faixa 2 = 64,00 x 7,5% = 4,80.
        Assert.Equal("Parte acima de R$ 2.428,80: R$ 2.492,80 (base) - R$ 2.428,80 = R$ 64,00; R$ 64,00 x 7,5% = R$ 4,80",
            Formula(resultado, "IRRF sobre o 13º por faixas (desconto simplificado)", "Faixa 2 (7,5%)"));
    }

    [Fact]
    public async Task Complemento_do_13o_detalha_o_13o_revisado_so_no_recalculo()
    {
        var caso = new SimularComplementoDecimoTerceiroUseCase(await Ambiente.ConsultaAsync());
        Task<DemonstrativoDto> SimularAsync(TributacaoComplementoDecimoTerceiro tributacao) => caso.ExecutarAsync(new SimularComplementoDecimoTerceiroRequest(
            new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 1), 3_000m, 1_000m, 11_000m, 2_200m, 12, 0, tributacao), default).Sucesso();

        // 13º revisado de 4.100,00 pela tabela de 2025: (4.100,00 - 2.793,88) x 12% = 156,7344 → 156,73; total 385,40.
        var recalculo = await SimularAsync(TributacaoComplementoDecimoTerceiro.Recalculo);
        Assert.Equal("Parte acima de R$ 2.793,88: R$ 4.100,00 (base) - R$ 2.793,88 = R$ 1.306,12; R$ 1.306,12 x 12% = R$ 156,7344, truncado em R$ 156,73",
            Formula(recalculo, "INSS sobre o 13º revisado", "Faixa 3 (12%)"));
        // IRRF pelo desconto simplificado: base 3.492,80; faixa 3 = 666,15 x 15% = 99,9225; 3.492,80 x 15% - 394,16 = 129,76.
        const string faixas = "IRRF sobre o 13º revisado por faixas (desconto simplificado)";
        Assert.Equal("Parte acima de R$ 2.826,65: R$ 3.492,80 (base) - R$ 2.826,65 = R$ 666,15; R$ 666,15 x 15% = R$ 99,9225",
            Formula(recalculo, faixas, "Faixa 3 (15%)"));
        Assert.Contains("= R$ 129,76", Formula(recalculo, faixas, "Pela parcela a deduzir"));

        var rra = await SimularAsync(TributacaoComplementoDecimoTerceiro.Rra);
        Assert.Contains(rra.Memoria, grupo => grupo.Titulo == "INSS sobre o 13º revisado");
        Assert.DoesNotContain(rra.Memoria, grupo => grupo.Titulo.StartsWith("IRRF sobre o 13º revisado"));
    }
}
