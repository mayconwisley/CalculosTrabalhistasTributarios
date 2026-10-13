using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Fracionamento das férias em até três períodos (CLT, art. 134, § 1º) e férias em dobro (art. 137).</summary>
public class FeriasFracionadasTests
{
    private static readonly DateOnly Outubro = new(2026, 10, 1);

    private static async Task<SimularFeriasUseCase> CasoAsync() => new(await Ambiente.ConsultaAsync());

    private static SimularFeriasRequest Pedido(int dias, int[] outros, DateOnly? inicio = null, DateOnly? fimConcessivo = null) =>
        new(Outubro, 3000m, 0m, 0, dias, false, false, 0, InicioGozo: inicio, OutrosPeriodos: outros, FimPeriodoConcessivo: fimConcessivo);

    [Theory]
    [InlineData(10, new[] { 14, 6 })]   // 10 + 14 + 6 = 30, um período de 14
    [InlineData(10, new int[0])]         // sobram 20, que formam o período de 14 ou mais
    [InlineData(10, new[] { 5 })]        // sobram 15 para o terceiro período, que terá os 14 dias
    [InlineData(14, new[] { 8 })]        // sobram 8 para o terceiro período
    [InlineData(10, new[] { 6 })]        // sobram 14, que formam o terceiro período com os 14 dias
    public async Task Divisao_valida_das_ferias(int dias, int[] outros) =>
        Assert.True((await (await CasoAsync()).ExecutarAsync(Pedido(dias, outros), default)).Sucesso);

    [Theory]
    [InlineData(10, new[] { 10, 10 }, "14 dias")]      // nenhum período de 14 dias
    [InlineData(27, new int[0], "Sobram 3 dia(s)")]     // o saldo de 3 dias não forma período
    [InlineData(10, new[] { 14, 4 }, "pelo menos 5 dias")]
    [InlineData(20, new[] { 14 }, "passam dos 30")]
    [InlineData(14, new[] { 10, 5 }, "Sobram 1 dia(s)")] // três períodos e ainda sobra 1 dia
    public async Task Divisao_invalida_das_ferias(int dias, int[] outros, string trecho)
    {
        var resultado = await (await CasoAsync()).ExecutarAsync(Pedido(dias, outros), default);
        Assert.True(resultado.Falhou);
        Assert.Contains(trecho, resultado.Erro.Mensagem);
    }

    [Fact]
    public async Task Memoria_mostra_os_tres_periodos()
    {
        var demonstrativo = await (await CasoAsync()).ExecutarAsync(Pedido(10, [14, 6]), default).Sucesso();
        Assert.Contains(demonstrativo.Memoria[0].Formulas, formula => formula.Formula.Contains("períodos de 10 + 14 + 6 = 30 dias"));
    }

    [Fact]
    public async Task Dias_depois_do_periodo_concessivo_sao_pagos_em_dobro_sem_inss_e_sem_fgts()
    {
        var caso = await CasoAsync();
        // Gozo de 20/10/2026 a 18/11/2026; o período concessivo terminou em 31/10/2026: 18 dias em dobro.
        var emDobro = await caso.ExecutarAsync(Pedido(30, [], new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 31)), default).Sucesso();
        var noPrazo = await caso.ExecutarAsync(Pedido(30, [], new DateOnly(2026, 10, 20)), default).Sucesso();

        // 3.000,00 ÷ 30 x 18 = 1.800,00; o terço, 600,00.
        Assert.Contains(emDobro.Proventos, verba => verba.Descricao == "Dobra das férias fora do período concessivo" && verba.Valor == 1800m && verba.Referencia == "18 dias");
        Assert.Contains(emDobro.Proventos, verba => verba.Descricao == "1/3 sobre a dobra das férias" && verba.Valor == 600m);
        Assert.Equal(Valor(noPrazo.Descontos, "INSS"), Valor(emDobro.Descontos, "INSS"));
        Assert.Equal(320m, emDobro.Informativos.Single().Valor);   // FGTS de 8% sobre 4.000,00, sem a dobra
        Assert.True(Valor(emDobro.Descontos, "IRRF") > Valor(noPrazo.Descontos, "IRRF"));
    }

    [Fact]
    public async Task Gozo_dentro_do_prazo_nao_tem_dobra_e_inicio_na_sexta_e_avisado()
    {
        // 23/10/2026 é sexta-feira; o gozo de 10 dias termina em 01/11/2026, dentro do prazo até 30/11/2026.
        var demonstrativo = await (await CasoAsync()).ExecutarAsync(Pedido(10, [], new DateOnly(2026, 10, 23), new DateOnly(2026, 11, 30)), default).Sucesso();
        Assert.DoesNotContain(demonstrativo.Proventos, verba => verba.Descricao.StartsWith("Dobra"));
        Assert.Contains(demonstrativo.Observacoes, observacao => observacao.Contains("não há dobra"));
        Assert.Contains(demonstrativo.Observacoes, observacao => observacao.Contains("art. 134, § 3º"));
    }

    [Fact]
    public async Task Fim_do_periodo_concessivo_exige_o_inicio_do_gozo()
    {
        var resultado = await (await CasoAsync()).ExecutarAsync(Pedido(30, [], fimConcessivo: new DateOnly(2026, 10, 31)), default);
        Assert.Contains("início do gozo", resultado.Erro.Mensagem);
    }

    private static decimal Valor(IReadOnlyList<VerbaDto> verbas, string inicio) => verbas.Single(verba => verba.Descricao.StartsWith(inicio)).Valor;
}
