using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Domain.Previdencia.Aposentadoria;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Regras de aposentadoria da EC 103/2019 com datas e valores conferidos à mão; mínimo de 1.621,00 e teto de 8.475,55.</summary>
public class AposentadoriaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 13);

    private static ApuracaoAposentadoria Simular(SexoSegurado sexo, DateOnly nascimento, int meses, int mesesReforma, decimal media = 5000m, bool continua = true, decimal? expectativa = null) =>
        SimuladorAposentadoria.Simular(new EntradaAposentadoria(sexo, nascimento, Hoje, meses, mesesReforma, continua, media, 1621m, 8475.55m, expectativa)).Sucesso();

    private static ResultadoRegraAposentadoria Regra(ApuracaoAposentadoria a, RegraAposentadoria regra) => a.Regras.Single(r => r.Regra == regra);

    [Fact]
    public void Homem_com_33_anos_de_contribuicao_projeta_cada_regra()
    {
        // Nascido em 15/03/1964: 62 anos e 6 meses hoje; 33 anos de contribuição hoje e 26 anos e 1 mês em 13/11/2019.
        var a = Simular(SexoSegurado.Masculino, new DateOnly(1964, 3, 15), 396, 313);

        // Idade progressiva: 35 anos em 10/2028, mas 65 anos (o limite desde 2027) só em 04/2029; 35 anos e 6 meses: 90%.
        var idade = Regra(a, RegraAposentadoria.IdadeProgressiva);
        Assert.Equal((new DateOnly(2029, 4, 13), 780, 426, 90m, 4500m), (idade.Data, idade.IdadeMeses, idade.TempoMeses, idade.Coeficiente, idade.Valor));
        // Pontos: 1.146 + 2 x meses >= 105 pontos (1.260 meses) em 57 meses, 07/2031; 37 anos de contribuição: 94%.
        var pontos = Regra(a, RegraAposentadoria.Pontos);
        Assert.Equal((new DateOnly(2031, 7, 13), 94m, 4700m), (pontos.Data, pontos.Coeficiente, pontos.Valor));
        // Pedágio de 100%: faltavam 107 meses em 2019; 35 anos + 107 meses = 527 meses em 131 meses, 09/2037; 100% da média.
        var pedagio = Regra(a, RegraAposentadoria.Pedagio100);
        Assert.Equal((new DateOnly(2037, 9, 13), 527, 5000m), (pedagio.Data, pedagio.TempoMeses, pedagio.Valor));
        // Transição por idade: 65 anos e 15 de contribuição, também em 04/2029.
        Assert.Equal(new DateOnly(2029, 4, 13), Regra(a, RegraAposentadoria.IdadeTransicao).Data);
        Assert.False(Regra(a, RegraAposentadoria.Pedagio50).Aplicavel);   // tinha menos de 33 anos em 2019
        Assert.False(Regra(a, RegraAposentadoria.Permanente).Aplicavel);  // contribuía antes da reforma

        Assert.Equal(RegraAposentadoria.IdadeProgressiva, a.Primeira?.Regra);
        Assert.Equal(RegraAposentadoria.Pedagio100, a.MaiorValor?.Regra);
    }

    [Fact]
    public void Pedagio_de_50_com_fator_previdenciario()
    {
        // Nascido em 01/01/1970, 34 anos em 13/11/2019 (faltava 1 ano: pedágio de 6 meses) e 40 anos e 11 meses hoje: já cumpre.
        // Fator: (40,9167 x 0,31 ÷ 20) x [1 + (56,75 + 40,9167 x 0,31) ÷ 100] = 1,0746; 5.000,00 x 1,0746 = 5.373,00.
        var a = Simular(SexoSegurado.Masculino, new DateOnly(1970, 1, 1), 491, 408, expectativa: 20m);
        var pedagio = Regra(a, RegraAposentadoria.Pedagio50);

        Assert.Equal((Hoje, 1.0746m, 5373m), (pedagio.Data, pedagio.Fator, pedagio.Valor));
        Assert.Null(Regra(Simular(SexoSegurado.Masculino, new DateOnly(1970, 1, 1), 491, 408), RegraAposentadoria.Pedagio50).Valor);   // sem a expectativa
    }

    [Fact]
    public void Mulher_tem_coeficiente_acima_de_15_anos_e_pontos_proprios()
    {
        // Nascida em 10/05/1968: 58 anos e 5 meses hoje; 28 anos de contribuição hoje e 21 anos e 1 mês em 2019.
        var a = Simular(SexoSegurado.Feminino, new DateOnly(1968, 5, 10), 336, 253, 3000m);

        // Idade progressiva: 30 anos em 10/2028, quando tem 60 anos e 5 meses; em 2028 exige 60 anos e 6 meses (56 + 9 x 6 meses),
        // cumpridos em 11/2028, com 30 anos e 1 mês de contribuição: 60 + 2 x 15 = 90%.
        var idade = Regra(a, RegraAposentadoria.IdadeProgressiva);
        Assert.Equal((new DateOnly(2028, 11, 13), 90m, 2700m), (idade.Data, idade.Coeficiente, idade.Valor));
        // Transição por idade da mulher: 62 anos em 05/2030, com 31 anos e 7 meses: 60 + 2 x 16 = 92%.
        var transicao = Regra(a, RegraAposentadoria.IdadeTransicao);
        Assert.Equal((new DateOnly(2030, 5, 13), 92m), (transicao.Data, transicao.Coeficiente));
    }

    [Fact]
    public void Filiado_depois_da_reforma_so_tem_a_regra_permanente()
    {
        // Mulher nascida em 13/10/1966, com 3 anos de contribuição, todos depois de 2019: 62 anos já cumpridos, faltam 12 anos.
        var a = Simular(SexoSegurado.Feminino, new DateOnly(1966, 10, 13), 36, 0, 1000m);
        var permanente = Regra(a, RegraAposentadoria.Permanente);

        Assert.Equal((new DateOnly(2038, 10, 13), 180, 60m, 1621m), (permanente.Data, permanente.TempoMeses, permanente.Coeficiente, permanente.Valor));   // 600,00 sobe ao mínimo
        Assert.All(a.Regras.Where(r => r.Regra != RegraAposentadoria.Permanente), r => Assert.False(r.Aplicavel));
    }

    [Fact]
    public void Sem_novas_contribuicoes_o_tempo_fica_parado()
    {
        var a = Simular(SexoSegurado.Masculino, new DateOnly(1964, 3, 15), 396, 313, continua: false);
        Assert.Null(Regra(a, RegraAposentadoria.IdadeProgressiva).Data);   // nunca chega a 35 anos
        Assert.Equal((new DateOnly(2029, 4, 13), 396), (Regra(a, RegraAposentadoria.IdadeTransicao).Data, Regra(a, RegraAposentadoria.IdadeTransicao).TempoMeses));
    }

    [Fact]
    public void Beneficio_fica_no_teto()
    {
        var a = Simular(SexoSegurado.Masculino, new DateOnly(1964, 3, 15), 396, 313, media: 10000m);
        Assert.Equal(8475.55m, Regra(a, RegraAposentadoria.Pedagio100).Valor);
    }

    [Theory]
    [InlineData(1964, 700, 313, "passa da idade")]
    [InlineData(1964, 300, 313, "não pode ser negativo nem maior")]
    [InlineData(2015, 0, 0, "de 14 a 100 anos")]
    public void Entradas_invalidas_sao_recusadas(int ano, int meses, int mesesReforma, string trecho)
    {
        var entrada = new EntradaAposentadoria(SexoSegurado.Masculino, new DateOnly(ano, 3, 15), Hoje, meses, mesesReforma, true, 5000m, 1621m, 8475.55m);
        Assert.Contains(trecho, SimuladorAposentadoria.Simular(entrada).Erro.Mensagem);
    }
}
