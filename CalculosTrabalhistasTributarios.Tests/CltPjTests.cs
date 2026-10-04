using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class CltPjTests
{
    private static async Task<DemonstrativoDto> CalcularAsync(decimal valorPj, TributacaoPj tributacao = TributacaoPj.AnexoIIIFatorR, decimal custos = 300m) =>
        await new SimularCltPjUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularCltPjRequest(new DateOnly(2026, 10, 1), 6000m, 0, 800m, RegimeTributario.LucroRealOuPresumido, valorPj, tributacao, custos), default).Sucesso();

    private static decimal Valor(DemonstrativoDto resultado, string linha, int coluna) =>
        decimal.Parse(Texto.Normalizar(resultado.Comparativo!.Linhas.Single(item => item.Descricao == linha).Valores[coluna]).Replace("R$ ", ""), NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.GetCultureInfo("pt-BR"));

    [Fact]
    public async Task Ano_de_clt_com_ferias_e_13o()
    {
        var resultado = await CalcularAsync(10_000m);
        // 6.000 x 11 + 8.000 (férias com 1/3) + 6.000 (13º).
        Assert.Equal(80_000m, Valor(resultado, "Recebido no ano (bruto)", 0));
        // INSS de 2026: 641,50 por mês, 921,50 nas férias e 641,50 no 13º.
        Assert.Equal(8619.50m, Valor(resultado, "INSS", 0));
        // IRRF com a redução de 2026: 385,11 por mês e no 13º; 1.037,86 nas férias, acima de R$ 7.350,00.
        Assert.Equal(5659.18m, Valor(resultado, "IRRF", 0));
        Assert.Equal(6400m, Valor(resultado, "FGTS depositado", 0));
        Assert.Equal(81_721.32m, Valor(resultado, "Total para o profissional", 0));
        // Lucro Real: 20% + 2% + 5,8% + 8% sobre 80.000, mais 9.600 de benefícios.
        Assert.Equal(118_240m, Valor(resultado, "Custo para a empresa no ano", 0));
    }

    [Fact]
    public async Task Pj_no_fator_r_paga_6_por_cento_e_inss_do_pro_labore()
    {
        var resultado = await CalcularAsync(10_000m);
        Assert.Equal(7200m, Valor(resultado, "Simples Nacional (DAS)", 1));
        // Pró-labore de 28% (2.800,00): INSS de 308,00 e IRRF zero, pelo desconto simplificado.
        Assert.Equal(3696m, Valor(resultado, "INSS", 1));
        Assert.Equal(0m, Valor(resultado, "IRRF", 1));
        Assert.Equal(105_504m, Valor(resultado, "Total para o profissional", 1));
        Assert.Equal(23_782.68m, Valor(resultado, "Total para o profissional", 2));
    }

    [Fact]
    public async Task Anexo_v_e_segunda_faixa_do_anexo_iii()
    {
        var anexoV = await CalcularAsync(10_000m, TributacaoPj.AnexoV);
        Assert.Equal(1550m * 12, Valor(anexoV, "Simples Nacional (DAS)", 1));
        Assert.Equal(178.31m * 12, Valor(anexoV, "INSS", 1));
        // Receita de 240.000,00: (240.000 x 11,2% - 9.360) ÷ 240.000 = 7,3%.
        var faixa2 = await CalcularAsync(20_000m);
        Assert.Equal(1460m * 12, Valor(faixa2, "Simples Nacional (DAS)", 1));
    }

    [Fact]
    public async Task Valor_equivalente_iguala_o_total_do_clt()
    {
        var resultado = await CalcularAsync(0m);
        var equivalente = decimal.Parse(Texto.Normalizar(resultado.Destaques[3].Valor).Replace("R$ ", ""), NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"));
        Assert.True(Valor(resultado, "Total para o profissional", 1) >= 81_721.32m);
        var umCentavoAbaixo = await CalcularAsync(equivalente - 0.01m);
        Assert.True(Valor(umCentavoAbaixo, "Total para o profissional", 1) < 81_721.32m);
    }

    [Theory]
    [InlineData(AnexoSimples.III, 180_000, 6)]
    [InlineData(AnexoSimples.III, 240_000, 7.3)]
    [InlineData(AnexoSimples.V, 240_000, 16.125)]
    [InlineData(AnexoSimples.III, 4_800_000, 19.5)]
    public void Aliquota_efetiva_do_simples(AnexoSimples anexo, decimal receita, decimal efetiva) =>
        Assert.Equal(efetiva, SimplesNacional.Aliquota(anexo, receita).Sucesso().AliquotaEfetiva);

    [Fact]
    public async Task Custo_anual_do_funcionario_soma_13_salarios_e_um_terco()
    {
        var resultado = await new SimularCustoFuncionarioUseCase().ExecutarAsync(new SimularCustoFuncionarioRequest(3000m, RegimeTributario.LucroRealOuPresumido, 2m, 1.2345m, 5.8m, 0m, true), default).Sucesso();
        // 13,33 salários (40.000,00) com 36,269% de encargos, com o arredondamento das provisões.
        Assert.Equal("R$ 54.507,57", Texto.Normalizar(resultado.Destaques[1].Valor));
    }
}
