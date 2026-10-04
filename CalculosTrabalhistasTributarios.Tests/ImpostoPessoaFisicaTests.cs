using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Carnê-leão e ganho de capital da pessoa física.</summary>
public class ImpostoPessoaFisicaTests
{
    private static readonly DateOnly Outubro2026 = new(2026, 10, 1);

    private static async Task<DemonstrativoDto> CarneLeaoAsync(decimal rendimentos, decimal alugueis = 0m, decimal despesas = 0m, decimal livroCaixa = 0m, decimal inss = 0m) =>
        await new SimularCarneLeaoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularCarneLeaoRequest(Outubro2026, rendimentos, alugueis, despesas, livroCaixa, inss, 0, 0m), default).Sucesso();

    private static async Task<TabelasDaCompetencia> TabelasAsync() => await (await Ambiente.ConsultaAsync()).ObterTabelasAsync(Outubro2026, default).Sucesso();

    [Fact]
    public async Task Carne_leao_tributa_o_aluguel_liquido_com_o_livro_caixa_e_o_inss()
    {
        var resultado = await CarneLeaoAsync(10_000m, alugueis: 3_000m, despesas: 500m, livroCaixa: 1_000m, inss: 800m);
        var esperado = (await TabelasAsync()).CalcularIrrf(12_500m, 800m, 0, tributacaoExclusiva: true, livroCaixa: 1_000m).Imposto;

        Assert.Equal(esperado, resultado.Descontos.Single(verba => verba.Descricao.StartsWith("Carnê-leão", StringComparison.Ordinal)).Valor);
        Assert.Equal("R$ 12.500,00", resultado.Destaques[2].Valor);
        Assert.Contains("30/11/2026", resultado.Referencia);
    }

    [Fact]
    public async Task Livro_caixa_so_abate_rendimentos_de_trabalho()
    {
        var resultado = await CarneLeaoAsync(0m, alugueis: 6_000m, livroCaixa: 2_000m);

        Assert.Contains(resultado.Observacoes, observacao => observacao.Contains("passou dos rendimentos de trabalho"));
    }

    [Fact]
    public async Task Imposto_abaixo_de_10_reais_passa_para_o_mes_seguinte()
    {
        var imposto = (await TabelasAsync()).CalcularIrrf(5_010m, 0m, 0, tributacaoExclusiva: true).Imposto;
        Assert.InRange(imposto, 0.01m, 9.99m);

        var resultado = await CarneLeaoAsync(5_010m);

        Assert.Equal("Acumula", resultado.Destaques[0].Valor);
    }

    private static ApuracaoGanhoDeCapital Ganho(BemAlienado bem, string compra, string venda, decimal custo, decimal valorVenda, bool unico = false, decimal reinvestido = 0m) =>
        CalculadoraGanhoDeCapital.Calcular(new AlienacaoDeBem(bem, DateOnly.Parse(compra), DateOnly.Parse(venda), custo, valorVenda, 0m, unico, reinvestido, 0m)).Sucesso();

    [Fact]
    public void Outro_bem_paga_15_por_cento_do_ganho() =>
        Assert.Equal(10_500.00m, Ganho(BemAlienado.OutroBem, "2020-01-10", "2026-10-10", 50_000m, 120_000m).Imposto);

    [Fact]
    public void Venda_de_ate_35_mil_no_mes_e_isenta() =>
        Assert.NotNull(Ganho(BemAlienado.OutroBem, "2020-01-10", "2026-10-10", 10_000m, 30_000m).Isencao);

    [Fact]
    public void Unico_imovel_de_ate_440_mil_e_isento() =>
        Assert.Equal(0m, Ganho(BemAlienado.ImovelResidencial, "2015-01-10", "2026-10-10", 200_000m, 430_000m, unico: true).Imposto);

    [Fact]
    public void Imovel_comprado_depois_de_2005_tem_so_o_fr2()
    {
        // 120 meses de 01/2010 a 01/2020: FR2 = 1 ÷ 1,0035^120 = 0,657529.
        var ganho = Ganho(BemAlienado.OutroImovel, "2010-01-15", "2020-01-20", 500_000m, 1_000_000m);

        Assert.Equal(0.657529m, ganho.Fr2);
        Assert.Equal(328_764.50m, ganho.GanhoTributavel);
        Assert.Equal(49_314.68m, ganho.Imposto);
    }

    [Fact]
    public void Imovel_de_1985_tem_reducao_de_20_por_cento_e_os_dois_fatores()
    {
        // 20% (Lei 7.713/1988, art. 18), FR1 de 118 meses (01/1996 a 11/2005) e FR2 de 250 meses (12/2005 a 10/2026).
        var ganho = Ganho(BemAlienado.OutroImovel, "1985-06-01", "2026-10-15", 200_000m, 500_000m);

        Assert.Equal(20m, ganho.PercentualReducao1988);
        Assert.Equal(118, ganho.MesesFr1);
        Assert.Equal(250, ganho.MesesFr2);
        Assert.Equal(49_465.82m, ganho.GanhoTributavel);
        Assert.Equal(7_419.87m, ganho.Imposto);
    }

    [Fact]
    public void Reinvestimento_em_residencial_isenta_a_parte_aplicada()
    {
        var ganho = Ganho(BemAlienado.ImovelResidencial, "2015-03-10", "2026-03-10", 400_000m, 800_000m, reinvestido: 400_000m);

        Assert.Equal(0.5m, ganho.ProporcaoReinvestida);
        Assert.Equal(126_106.20m, ganho.GanhoTributavel);
    }

    [Fact]
    public void Ganho_acima_de_5_milhoes_tem_aliquotas_progressivas() =>
        // 5 milhões a 15% e 1 milhão a 17,5%.
        Assert.Equal(925_000m, Ganho(BemAlienado.OutroBem, "2020-01-10", "2026-10-10", 1_000_000m, 7_000_000m).Imposto);
}
