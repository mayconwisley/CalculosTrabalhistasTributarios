using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculadoraRra = CalculosTrabalhistasTributarios.Domain.Tributacao.CalculadoraRra;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// IRRF sobre rendimentos recebidos acumuladamente (IN RFB 1.500/2014, arts. 36 a 45). Os valores esperados foram
/// calculados à mão pela média mensal (base ÷ NM na tabela mensal, vezes NM), que equivale à tabela acumulada do Anexo IV.
/// </summary>
public class RraTests
{
    private static PagamentoRra Pagamento(DateOnly data, IReadOnlyList<ParcelaRra> parcelas, decimal correcao = 0m, decimal juros = 0m,
        decimal despesas = 0m, decimal inssAnteriores = 0m, decimal pensaoAnteriores = 0m, decimal inssAno = 0m, decimal pensaoAno = 0m,
        decimal? totalParcelado = null, bool reducao = true, decimal outros = 0m, decimal inssOutros = 0m, int dependentes = 0,
        OrigemPagamentoRra origem = OrigemPagamentoRra.FontePagadora) =>
        new(data, origem, parcelas, correcao, juros, despesas, inssAnteriores, pensaoAnteriores, inssAno, pensaoAno, totalParcelado, reducao, outros, inssOutros, dependentes);

    private static ParcelaRra[] Meses(int ano, int primeiro, int ultimo, decimal valor) =>
        Enumerable.Range(primeiro, ultimo - primeiro + 1).Select(mes => new ParcelaRra(new DateOnly(ano, mes, 1), TipoParcelaRra.Mensal, valor)).ToArray();

    private static async Task<DemonstrativoDto> SimularAsync(PagamentoRra pagamento) =>
        (await new SimularRraUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularRraRequest(pagamento), default)).Sucesso();

    private static async Task<ApuracaoRra> ApurarAsync(PagamentoRra pagamento)
    {
        var mes = new DateOnly(pagamento.DataPagamento.Year, pagamento.DataPagamento.Month, 1);
        var tabelas = await (await Ambiente.ConsultaAsync()).ObterTabelasAsync(mes, default).Sucesso();
        return CalculadoraRra.Calcular(pagamento, tabelas).Sucesso();
    }

    [Fact]
    public async Task Anos_anteriores_usam_a_tabela_multiplicada_pelos_meses_com_o_13o_contando_um_mes()
    {
        // 12 meses de 2024 + 13º = NM 13; base 39.000 - 3.000 de INSS = 36.000.
        // Média 2.769,23 na faixa de 7,5% (tabela de 05/2025): 36.000 × 7,5% - 182,16 × 13 = 331,92.
        var rra = await ApurarAsync(Pagamento(new DateOnly(2025, 12, 10),
            [.. Meses(2024, 1, 12, 3_000m), new(new DateOnly(2024, 12, 1), TipoParcelaRra.DecimoTerceiro, 3_000m)], inssAnteriores: 3_000m));

        var anteriores = Assert.IsType<ParteAnosAnterioresRra>(rra.AnosAnteriores);
        Assert.Equal(13, anteriores.MesesTotais);
        Assert.Equal(36_000m, anteriores.Irrf.BaseCalculo);
        Assert.Equal(331.92m, anteriores.Irrf.Imposto);
        Assert.False(anteriores.Irrf.ReducaoAplicavel);
        Assert.Null(rra.AnoPagamento);
    }

    [Fact]
    public async Task Em_2026_a_parcela_a_deduzir_e_a_exata_do_anexo_iv_e_a_reducao_e_proporcional_aos_meses()
    {
        // NM 10, base 60.000: 60.000 × 27,5% - 908,72150 × 10 = 7.412,785 → 7.412,79.
        // Média de 6.000: redução de 978,62 - 0,133145 × 6.000 = 179,75 por mês, × 10 = 1.797,50.
        var parcelas = Meses(2025, 1, 10, 6_000m);
        var com = (await ApurarAsync(Pagamento(new DateOnly(2026, 3, 15), parcelas))).AnosAnteriores!.Irrf;
        var sem = (await ApurarAsync(Pagamento(new DateOnly(2026, 3, 15), parcelas, reducao: false))).AnosAnteriores!.Irrf;

        Assert.Equal(7_412.79m, com.ImpostoAntesReducao);
        Assert.Equal(908.7215m, com.ParcelaADeduzirMensal);
        Assert.Equal(1_797.50m, com.Reducao);
        Assert.Equal(5_615.29m, com.Imposto);
        Assert.Equal(7_412.79m, sem.Imposto);
        Assert.Equal(0m, sem.Reducao);
    }

    [Fact]
    public async Task Pagamento_misto_separa_as_partes_rateia_correcao_e_despesas_e_isenta_os_juros()
    {
        // Parcelas: 12/2025 e 13º de 2025 (4.000 cada) e 01 e 02/2026 (4.000 cada). Correção 1.000 → 500 para cada parte.
        // Total recebido 19.000 com 2.000 de juros; despesas de 1.900 → 850 para cada parte e 200 dos juros, não dedutíveis.
        var pagamento = Pagamento(new DateOnly(2026, 5, 20),
            [new(new DateOnly(2025, 12, 1), TipoParcelaRra.Mensal, 4_000m), new(new DateOnly(2025, 12, 1), TipoParcelaRra.DecimoTerceiro, 4_000m),
             .. Meses(2026, 1, 2, 4_000m)],
            correcao: 1_000m, juros: 2_000m, despesas: 1_900m, outros: 5_000m, inssOutros: 500m);
        var rra = await ApurarAsync(pagamento);

        var anteriores = rra.AnosAnteriores!;
        Assert.Equal((500m, 850m, 2), (anteriores.Correcao, anteriores.Despesas, anteriores.MesesTotais));
        Assert.Equal(7_650m, anteriores.Irrf.BaseCalculo);
        // Média de 3.825 na faixa de 22,5%: 185,1375 × 2 = 370,28; rendimentos de até 5.000 × 2 zeram o imposto.
        Assert.Equal(370.28m, anteriores.Irrf.ImpostoAntesReducao);
        Assert.Equal(0m, anteriores.Irrf.Imposto);
        Assert.Equal(200m, rra.DespesasNaoDedutiveis);
        Assert.Equal(19_000m, rra.TotalRecebido);

        // Ano do pagamento: 8.500 - 850 + 5.000 de rendimentos normais = 12.650. Simplificado: 12.042,80 × 27,5% - 908,73 = 2.403,04.
        // Só os rendimentos normais: 5.000 - 607,20 = 4.392,80 × 22,5% - 675,49 = 312,89, zerado pela redução.
        var doAno = rra.AnoPagamento!;
        Assert.Equal(12_650m, doAno.ComOutros!.Rendimentos);
        Assert.Equal(2_403.04m, doAno.ComOutros.Imposto);
        Assert.Equal(0m, doAno.SoOutros!.Imposto);
        Assert.Equal(2_403.04m, doAno.Imposto);

        var demonstrativo = await SimularAsync(pagamento);
        Assert.Equal([("Rendimentos de anos anteriores", 8_500m), ("Rendimentos de 2026", 8_500m), ("Juros de mora", 2_000m)],
            demonstrativo.Proventos.Select(verba => (verba.Descricao, verba.Valor)));
        Assert.Equal(2_403.04m, demonstrativo.Descontos.Sum(verba => verba.Valor));
        Assert.Contains(demonstrativo.Memoria, grupo => grupo.Titulo == "RRA de anos anteriores");
        Assert.Contains(demonstrativo.Memoria, grupo => grupo.Titulo == "Rendimentos normais do mês, sem o RRA");
    }

    [Fact]
    public async Task Justica_federal_retem_3_por_cento_da_parte_do_ano_sem_deducoes()
    {
        var rra = await ApurarAsync(Pagamento(new DateOnly(2026, 4, 10), Meses(2026, 1, 2, 5_000m), despesas: 500m, origem: OrigemPagamentoRra.JusticaFederal));

        Assert.Null(rra.AnosAnteriores);
        Assert.Equal(300m, rra.AnoPagamento!.Imposto); // 10.000 × 3%.
        Assert.Null(rra.AnoPagamento.ComOutros);
    }

    [Fact]
    public async Task Parcela_de_rra_pago_em_meses_distintos_usa_meses_proporcionais_ao_valor()
    {
        // Art. 45, I: 12 meses × 12.000 ÷ 30.000 = 4,8.
        var rra = await ApurarAsync(Pagamento(new DateOnly(2026, 6, 1), Meses(2024, 1, 12, 1_000m), totalParcelado: 30_000m));

        Assert.Equal(12, rra.AnosAnteriores!.MesesTotais);
        Assert.Equal(4.8m, rra.AnosAnteriores.Irrf.Meses);
    }

    [Theory]
    [InlineData(2026, 7, "posterior ao mês do pagamento")]
    [InlineData(2025, 1, "INSS e a pensão do ano do pagamento")]
    public async Task Entradas_inconsistentes_sao_recusadas_com_orientacao(int ano, int mes, string trecho)
    {
        var pagamento = Pagamento(new DateOnly(2026, 6, 1), [new(new DateOnly(ano, mes, 1), TipoParcelaRra.Mensal, 1_000m)], inssAno: ano == 2025 ? 100m : 0m);

        var erro = (await new SimularRraUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularRraRequest(pagamento), default)).Falha();

        Assert.Contains(trecho, erro.Mensagem);
    }

    [Fact]
    public async Task Diferencas_de_reajuste_seguem_para_o_rra_por_competencia_com_o_13o_em_linha_propria()
    {
        var dezembro = new DateOnly(2025, 12, 1);
        var reajuste = (await new SimularReajusteRetroativoUseCase().ExecutarAsync(new SimularReajusteRetroativoRequest(
        [
            new(new DateOnly(2025, 11, 1), TipoParcelaReajuste.Salario, 1_000m, 1_100m),
            new(dezembro, TipoParcelaReajuste.Salario, 1_000m, 1_100m),
            new(dezembro, TipoParcelaReajuste.FeriasGozadas, 1_000m, 1_100m, 30),
            new(dezembro, TipoParcelaReajuste.DecimoTerceiro, 1_000m, 1_100m, 12)
        ]), default)).Sucesso();
        var ferias = reajuste.Proventos.Single(verba => verba.Descricao.StartsWith("Férias")).Valor;

        var parcelas = Assert.IsAssignableFrom<IReadOnlyList<ParcelaRra>>(reajuste.ParcelasRra);
        Assert.Equal(
            [new ParcelaRra(new DateOnly(2025, 11, 1), TipoParcelaRra.Mensal, 100m), new(dezembro, TipoParcelaRra.Mensal, 100m + ferias), new(dezembro, TipoParcelaRra.DecimoTerceiro, 100m)],
            parcelas);
        Assert.Equal(reajuste.TotalProventos, parcelas.Sum(item => item.Valor));

        var campo = new CampoParcelasRraViewModel();
        Assert.True(campo.Importar(CampoParcelasRraViewModel.CriarImportacao(parcelas)));
        Assert.Equal(parcelas, campo.Ler().Sucesso());
        Assert.Equal(("11/2025", "12/2025"), (campo.Inicio, campo.Fim));
    }

    [Fact]
    public void Formulario_gera_meses_recusa_competencia_repetida_e_preserva_as_linhas_no_historico()
    {
        var campo = new CampoParcelasRraViewModel { Inicio = "01/2024", Fim = "12/2024", ValorMensal = "2.000,00" };
        Assert.True(campo.Gerar());
        campo.AdicionarDecimoTerceiroCommand.Execute(null);
        campo.Parcelas[^1].Valor = "2.000,00";
        var lidas = campo.Ler().Sucesso();
        Assert.Equal(13, lidas.Count);
        Assert.Equal(TipoParcelaRra.DecimoTerceiro, lidas[^1].Tipo);
        Assert.Equal(new DateOnly(2024, 12, 1), lidas[^1].Competencia);

        var reaberto = new CampoParcelasRraViewModel();
        Assert.True(reaberto.Importar(campo.Exportar()));
        Assert.Equal(lidas, reaberto.Ler().Sucesso());
        Assert.False(reaberto.Importar("{inválido"));
        Assert.Equal(13, reaberto.Parcelas.Count);

        reaberto.Parcelas[1].Competencia = "01/2024";
        Assert.Contains("mais de uma vez", reaberto.Ler().Falha().Mensagem);
    }
}
