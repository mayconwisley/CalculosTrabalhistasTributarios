using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Atualizacao;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Tributo em atraso (Lei 9.430/1996, art. 61) e correção de valores por índice, com a Selic e o IPCA semeados.</summary>
public class AtualizacaoMonetariaTests
{
    private static async Task<DemonstrativoDto> TributoAsync(decimal principal, string vencimento, string pagamento) =>
        await new SimularTributoAtrasoUseCase(await Ambiente.IndicesAsync())
            .ExecutarAsync(new SimularTributoAtrasoRequest(GuiaDeRecolhimento.Darf, principal, DateOnly.Parse(vencimento), DateOnly.Parse(pagamento)), default).Sucesso();

    private static decimal Verba(DemonstrativoDto demonstrativo, string inicio) => demonstrativo.Proventos.Single(verba => verba.Descricao.StartsWith(inicio, StringComparison.Ordinal)).Valor;

    [Fact]
    public async Task Multa_limitada_a_20_por_cento_e_selic_dos_meses_entre_vencimento_e_pagamento()
    {
        // 85 dias x 0,33% = 28,05%, limitada a 20%. Selic de 02/2026 (1,00%) e 03/2026 (1,21%) mais 1% de 04/2026 = 3,21%.
        var guia = await TributoAsync(1_000m, "2026-01-20", "2026-04-15");

        Assert.Equal(200.00m, Verba(guia, "Multa"));
        Assert.Equal(32.10m, Verba(guia, "Juros"));
        Assert.Equal(1_232.10m, guia.Resultado);
    }

    [Fact]
    public async Task Pago_no_mes_do_vencimento_so_tem_multa()
    {
        // 10 dias x 0,33% = 3,30%.
        var guia = await TributoAsync(1_000m, "2026-03-10", "2026-03-20");

        Assert.Equal(33.00m, Verba(guia, "Multa"));
        Assert.Equal(0m, Verba(guia, "Juros"));
    }

    [Fact]
    public async Task Pago_no_mes_seguinte_tem_juros_de_1_por_cento()
    {
        // 11 dias x 0,33% = 3,63%; sem mês inteiro entre os dois, os juros são só o 1% do mês do pagamento.
        var guia = await TributoAsync(1_000m, "2026-03-25", "2026-04-05");

        Assert.Equal(36.30m, Verba(guia, "Multa"));
        Assert.Equal(10.00m, Verba(guia, "Juros"));
    }

    [Fact]
    public async Task Pago_no_vencimento_nao_tem_acrescimos()
    {
        var guia = await TributoAsync(1_000m, "2026-03-20", "2026-03-20");

        Assert.Equal(1_000m, guia.Resultado);
    }

    [Fact]
    public void Selic_que_falta_na_tabela_e_informada()
    {
        var erro = CalculadoraTributoEmAtraso.Calcular(1_000m, new DateOnly(2026, 1, 20), new DateOnly(2026, 5, 10), new Dictionary<DateOnly, decimal> { [new(2026, 2, 1)] = 1m }).Falha();

        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
        Assert.Contains("03/2026", erro.Mensagem);
    }

    [Fact]
    public async Task Correcao_pelo_ipca_de_um_ano_com_juros_e_multa()
    {
        // IPCA de 01 a 12/2025: fator 1,042644. Juros de 1% x 12 meses e multa de 2% sobre 1.042,64.
        var resultado = await new SimularCorrecaoValorUseCase(await Ambiente.IndicesAsync())
            .ExecutarAsync(new SimularCorrecaoValorRequest(1_000m, new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1), IndiceEconomico.Ipca, 1m, 2m), default).Sucesso();

        Assert.Equal(42.64m, Verba(resultado, "Correção"));
        Assert.Equal(125.12m, Verba(resultado, "Juros"));
        Assert.Equal(20.85m, Verba(resultado, "Multa"));
        Assert.Equal(1_188.61m, resultado.Resultado);
    }

    [Fact]
    public async Task Correcao_exige_mes_final_posterior()
    {
        var erro = await new SimularCorrecaoValorUseCase(await Ambiente.IndicesAsync())
            .ExecutarAsync(new SimularCorrecaoValorRequest(1_000m, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 1), IndiceEconomico.Ipca, 0m, 0m), default).Falha();

        Assert.Equal(TipoErro.Validacao, erro.Tipo);
    }
}
