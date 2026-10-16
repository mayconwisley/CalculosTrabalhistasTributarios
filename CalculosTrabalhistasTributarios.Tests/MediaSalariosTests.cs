using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Previdencia;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Média dos salários de contribuição corrigidos pelo INPC (EC 103/2019, art. 26; Lei 8.213/1991, art. 29-B), com valores
/// conferidos à mão: cada salário vezes o produto de (1 + INPC) da competência até o mês anterior ao da simulação,
/// arredondado aos centavos, e a soma dividida pela quantidade de meses.
/// </summary>
public class MediaSalariosTests
{
    private static DateOnly Mes(int ano, int mes) => new(ano, mes, 1);

    private static readonly Dictionary<DateOnly, decimal> UmPorCento = new() { [Mes(2026, 1)] = 1m, [Mes(2026, 2)] = 1m, [Mes(2026, 3)] = 1m };

    [Fact]
    public void Cada_mes_e_corrigido_ate_o_mes_anterior_ao_da_simulacao()
    {
        // Simulação em 04/2026 com 1% de INPC em 01, 02 e 03/2026. Fatores: 03 = 1,01; 02 = 1,0201; 01 = 1,030301.
        // 1.000 x 1,030301 = 1.030,30; 1.000 x 1,0201 = 1.020,10; 2.000 x 1,01 = 2.020,00; 04/2026, o próprio mês, sem correção.
        // Soma 7.070,40 ÷ 4 = 1.767,60.
        var m = CalculadoraMediaSalarios.Calcular(
            [new(Mes(2026, 1), Mes(2026, 2), 1000m), new(Mes(2026, 3), Mes(2026, 3), 2000m), new(Mes(2026, 4), Mes(2026, 4), 3000m)],
            new DateOnly(2026, 4, 20), UmPorCento).Sucesso();

        Assert.Equal([1030.30m, 1020.10m, 2020.00m, 3000m], m.Meses.Select(mes => mes.Corrigido));
        Assert.Equal((1.030301m, 1m), (m.Meses[0].Fator, m.Meses[3].Fator));
        Assert.Equal((7070.40m, 1767.60m, 4), (m.Soma, m.Media, m.Quantidade));
        Assert.Equal((Mes(2026, 3), (DateOnly?)null), (m.CorrigidoAte, m.FaltaInpcDesde));
    }

    [Fact]
    public void Ate_dois_meses_finais_sem_inpc_ficam_sem_correcao()
    {
        // INPC só até 01/2026 e simulação em 04/2026: faltam 02 e 03/2026, ainda não publicados.
        var inpc = new Dictionary<DateOnly, decimal> { [Mes(2026, 1)] = 1m };
        var m = CalculadoraMediaSalarios.Calcular([new(Mes(2026, 1), Mes(2026, 2), 1000m)], new DateOnly(2026, 4, 1), inpc).Sucesso();

        Assert.Equal([1010m, 1000m], m.Meses.Select(mes => mes.Corrigido));
        Assert.Equal((Mes(2026, 1), Mes(2026, 2)), (m.CorrigidoAte, m.FaltaInpcDesde));
    }

    [Fact]
    public async Task Inpc_de_1994_da_tabela_de_indices()
    {
        // INPC de 07 a 12/1994 do IBGE: 7,75; 1,85; 1,40; 2,82; 2,96 e 1,70%. Produto: 1,198073; 1.000 x 1,198073 = 1.198,07.
        var inpc = await (await Ambiente.IndicesAsync()).ObterAsync(IndiceEconomico.Inpc, default);
        var m = CalculadoraMediaSalarios.Calcular([new(Mes(1994, 7), Mes(1994, 7), 1000m)], new DateOnly(1995, 1, 10), inpc).Sucesso();
        Assert.Equal(1198.07m, m.Media);
    }

    [Theory]
    [InlineData(1994, 6, 1994, 12, 1000, "desde 07/1994")]
    [InlineData(2026, 3, 2026, 1, 1000, "igual ou posterior ao início")]
    [InlineData(2026, 1, 2026, 5, 1000, "passa do mês da simulação")]
    [InlineData(2026, 1, 2026, 1, 0, "maior que zero")]
    public void Periodos_invalidos_sao_recusados(int anoInicio, int mesInicio, int anoFim, int mesFim, int salario, string trecho)
    {
        var resultado = CalculadoraMediaSalarios.Calcular([new(Mes(anoInicio, mesInicio), Mes(anoFim, mesFim), salario)], new DateOnly(2026, 4, 1), UmPorCento);
        Assert.Contains(trecho, resultado.Erro.Mensagem);
    }

    [Fact]
    public void Meses_repetidos_e_indices_faltando_sao_recusados()
    {
        var repetido = CalculadoraMediaSalarios.Calcular([new(Mes(2026, 1), Mes(2026, 2), 1000m), new(Mes(2026, 2), Mes(2026, 3), 1000m)], new DateOnly(2026, 4, 1), UmPorCento);
        Assert.Contains("02/2026 aparece em mais de um período", repetido.Erro.Mensagem);

        var lacuna = new Dictionary<DateOnly, decimal> { [Mes(2026, 1)] = 1m, [Mes(2026, 3)] = 1m };
        Assert.Contains("Falta o INPC de 02/2026", CalculadoraMediaSalarios.Calcular([new(Mes(2026, 1), Mes(2026, 1), 1000m)], new DateOnly(2026, 4, 1), lacuna).Erro.Mensagem);

        var desatualizada = CalculadoraMediaSalarios.Calcular([new(Mes(2026, 1), Mes(2026, 1), 1000m)], new DateOnly(2026, 4, 1), new Dictionary<DateOnly, decimal>());
        Assert.Contains("Faltam os índices do INPC de 01/2026 a 03/2026", desatualizada.Erro.Mensagem);
    }

    [Fact]
    public async Task Simulacao_usa_a_media_calculada_e_mostra_a_memoria()
    {
        // Simulação em 13/10/2026, salários de 3.000,00 de 06 a 08/2026; INPC de 06 a 08/2026: 0,14; -0,01 e -0,32%; o de
        // 09/2026 ainda não está na tabela. Fatores: 08 = 0,9968; 07 = 0,99670032; 06 = 0,998095700448.
        // Corrigidos: 2.994,29 + 2.990,10 + 2.990,40 = 8.974,79 ÷ 3 = 2.991,60.
        var caso = new SimularAposentadoriaUseCase(await Ambiente.ConsultaAsync(), await Ambiente.IndicesAsync());
        var pedido = new SimularAposentadoriaRequest(SexoSegurado.Masculino, new DateOnly(1964, 3, 15), new DateOnly(2026, 10, 13), 396, 313, true, 0m, null,
            [new(Mes(2026, 6), Mes(2026, 8), 3000m)]);
        var d = (await caso.ExecutarAsync(pedido, default)).Sucesso();

        Assert.Equal(("Média dos salários de contribuição", "R$ 2.991,60"), (d.Memoria[0].Titulo, Texto.Normalizar(d.Memoria[0].Destaque)));
        Assert.Equal("3 meses de R$ 3.000,00 x fatores de 0,998096 a 0,996800 = R$ 8.974,79", Texto.Normalizar(d.Memoria[0].Formulas[0].Formula));
        Assert.Contains(d.Observacoes, texto => texto.Contains("09/2026 em diante"));
        // 60% + 2 x 15 anos acima de 20 = 90% na idade progressiva: 2.991,60 x 90% = 2.692,44.
        Assert.Equal("R$ 2.692,44", Texto.Normalizar(d.Destaques[1].Valor));

        var excesso = await caso.ExecutarAsync(pedido with { MesesContribuicao = 2 }, default);
        Assert.Contains("mais que o tempo de contribuição informado", excesso.Erro.Mensagem);
    }
}
