using CalculosTrabalhistasTributarios.Domain.Previdencia;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>GPS do contribuinte individual e do facultativo, com salário mínimo de 1.621,00 e teto de 8.475,55.</summary>
public class ContribuicaoSeguradoTests
{
    private const decimal Minimo = 1621m;
    private const decimal Teto = 8475.55m;

    private static ApuracaoContribuicaoSegurado Calcular(CategoriaSegurado categoria, PlanoContribuicao plano, decimal salario = 0m,
        PeriodicidadeContribuicao periodicidade = PeriodicidadeContribuicao.Mensal, int ano = 2026, int mes = 10) =>
        CalculadoraContribuicaoSegurado.Calcular(Entrada(categoria, plano, salario, periodicidade, ano, mes)).Sucesso();

    private static EntradaContribuicaoSegurado Entrada(CategoriaSegurado categoria, PlanoContribuicao plano, decimal salario,
        PeriodicidadeContribuicao periodicidade, int ano = 2026, int mes = 10) =>
        new(categoria, plano, periodicidade, salario, CalculadoraContribuicaoSegurado.MesesDoPeriodo(new DateOnly(ano, mes, 1), periodicidade)
            .Select(competencia => new ParametrosContribuicaoMes(competencia, Minimo, Teto)).ToArray());

    [Fact]
    public void Plano_normal_do_autonomo_e_vencimento_adiado_do_domingo()
    {
        var c = Calcular(CategoriaSegurado.ContribuinteIndividual, PlanoContribuicao.Normal, 3000m);

        // 3.000,00 x 20% = 600,00; 15/11/2026 é domingo: vence na segunda, 16/11/2026.
        Assert.Equal((600m, "1007", new DateOnly(2026, 11, 16), 0m), (c.Total, c.Codigo, c.Vencimento, c.TotalComplementacao));
        Assert.Null(c.CodigoComplementacao);
    }

    [Fact]
    public void Base_acima_do_teto_e_limitada_e_sabado_passa_para_segunda()
    {
        var c = Calcular(CategoriaSegurado.Facultativo, PlanoContribuicao.Normal, 10000m, mes: 7);

        // 8.475,55 x 20% = 1.695,11; 15/08/2026 é sábado: vence em 17/08/2026.
        Assert.Equal((1695.11m, true, "1406", new DateOnly(2026, 8, 17)), (c.Total, c.Meses[0].LimitadaAoTeto, c.Codigo, c.Vencimento));
    }

    [Fact]
    public void Simplificado_do_facultativo_com_complementacao_de_9_por_cento()
    {
        var c = Calcular(CategoriaSegurado.Facultativo, PlanoContribuicao.Simplificado);

        // 1.621,00 x 11% = 178,31; complementação 1.621,00 x 9% = 145,89.
        Assert.Equal((178.31m, "1473", 145.89m, "1686", 9m), (c.Total, c.Codigo, c.TotalComplementacao, c.CodigoComplementacao, c.PercentualComplementacao));
    }

    [Fact]
    public void Simplificado_trimestral_soma_os_tres_meses_do_trimestre()
    {
        var c = Calcular(CategoriaSegurado.ContribuinteIndividual, PlanoContribuicao.Simplificado, periodicidade: PeriodicidadeContribuicao.Trimestral, mes: 2);

        // 1º trimestre: 3 x 178,31 = 534,93; vence em 15/04/2026, quarta-feira.
        Assert.Equal([new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)], c.Meses.Select(mes => mes.Parametros.Competencia));
        Assert.Equal((534.93m, "1180", new DateOnly(2026, 4, 15)), (c.Total, c.Codigo, c.Vencimento));
        Assert.Equal((437.67m, "1198"), (c.TotalComplementacao, c.CodigoComplementacao));
    }

    [Fact]
    public void Baixa_renda_e_de_5_por_cento_com_complementacao_de_15()
    {
        var c = Calcular(CategoriaSegurado.Facultativo, PlanoContribuicao.BaixaRenda);
        Assert.Equal((81.05m, "1929", 243.15m), (c.Total, c.Codigo, c.TotalComplementacao));   // 5% e 15% de 1.621,00
    }

    [Theory]
    [InlineData(CategoriaSegurado.ContribuinteIndividual, PlanoContribuicao.BaixaRenda, 0, PeriodicidadeContribuicao.Mensal, "só do facultativo de baixa renda")]
    [InlineData(CategoriaSegurado.Facultativo, PlanoContribuicao.Normal, 1620.99, PeriodicidadeContribuicao.Mensal, "no mínimo um salário mínimo (R$ 1.621,00)")]
    [InlineData(CategoriaSegurado.Facultativo, PlanoContribuicao.Normal, 2000, PeriodicidadeContribuicao.Trimestral, "só vale para quem contribui sobre o salário mínimo")]
    public void Entradas_invalidas_sao_recusadas(CategoriaSegurado categoria, PlanoContribuicao plano, double salario, PeriodicidadeContribuicao periodicidade, string trecho)
    {
        Assert.Contains(trecho, CalculadoraContribuicaoSegurado.Calcular(Entrada(categoria, plano, (decimal)salario, periodicidade)).Erro.Mensagem);
    }

    [Fact]
    public void Plano_normal_trimestral_sobre_o_minimo_e_aceito()
    {
        var c = Calcular(CategoriaSegurado.ContribuinteIndividual, PlanoContribuicao.Normal, Minimo, PeriodicidadeContribuicao.Trimestral, mes: 11);

        // 4º trimestre: 3 x 324,20 = 972,60; vence em 15/01/2027.
        Assert.Equal((972.60m, "1104", new DateOnly(2027, 1, 15)), (c.Total, c.Codigo, c.Vencimento));
    }
}
