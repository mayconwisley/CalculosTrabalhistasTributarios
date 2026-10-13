using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Novo afastamento pelo mesmo motivo em até 60 dias (Decreto 3.048/1999, art. 75, §§ 3º a 5º).</summary>
public class AfastamentoSucessivoTests
{
    private static readonly DateOnly Inicio = new(2026, 10, 1);

    private static async Task<DemonstrativoDto> CalcularAsync(AfastamentoAnterior? anterior) =>
        await new SimularAfastamentoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularAfastamentoRequest(TipoAfastamento.Doenca, Inicio, 30, 3000m, 3000m, false, anterior), default).Sucesso();

    private static decimal Empresa(DemonstrativoDto d) => d.Proventos.Where(verba => verba.Descricao is "15 primeiros dias (empresa)" or "Dias que completam os 15 (empresa)").Sum(verba => verba.Valor);

    private static int DiasInss(DemonstrativoDto d) => int.Parse(d.Proventos.Single(verba => verba.Descricao.StartsWith("Auxílio")).Referencia.Split(' ')[0]);

    [Fact]
    public async Task Dias_ja_pagos_pela_empresa_se_somam_em_ate_60_dias_do_retorno()
    {
        // Retorno em 11/09/2026, 20 dias antes do novo afastamento: 10 dias já pagos, a empresa completa 5 (3.000 ÷ 30 x 5 = 500).
        var d = await CalcularAsync(new AfastamentoAnterior(OrigemAfastamentoAnterior.Empresa, new DateOnly(2026, 9, 10), 10));
        Assert.Equal(500m, Empresa(d));
        Assert.Equal(25, DiasInss(d));
        Assert.Contains(d.Memoria[0].Formulas, formula => formula.Titulo == "Afastamento anterior pelo mesmo motivo" && formula.Formula.Contains("15 - 10 = 5 dias"));
    }

    [Fact]
    public async Task Beneficio_cessado_em_ate_60_dias_e_prorrogado_sem_pagamento_da_empresa()
    {
        // Benefício cessado em 15/08/2026, 47 dias antes: o INSS paga desde o primeiro dia.
        var d = await CalcularAsync(new AfastamentoAnterior(OrigemAfastamentoAnterior.BeneficioInss, new DateOnly(2026, 8, 15), 0));
        Assert.Equal(0m, Empresa(d));
        Assert.Equal(30, DiasInss(d));
        Assert.Equal("Nenhum dia", d.Destaques.Single(destaque => destaque.Rotulo == "Pago pela empresa").Complemento);
    }

    [Fact]
    public async Task Afastamento_anterior_ha_mais_de_60_dias_nao_se_soma()
    {
        // Retorno em 02/07/2026, 91 dias antes: a empresa paga de novo os 15 primeiros dias (3.000 ÷ 30 x 15 = 1.500).
        var d = await CalcularAsync(new AfastamentoAnterior(OrigemAfastamentoAnterior.Empresa, new DateOnly(2026, 7, 1), 10));
        Assert.Equal(1500m, Empresa(d));
        Assert.Contains(d.Observacoes, observacao => observacao.StartsWith("O afastamento anterior terminou há mais de 60 dias"));
    }

    [Theory]
    [InlineData(OrigemAfastamentoAnterior.Empresa, 2026, 10, 1, 10, "terminar antes")]
    [InlineData(OrigemAfastamentoAnterior.Empresa, 2026, 9, 10, 16, "de 1 a 15 dias")]
    public void Afastamento_anterior_invalido(OrigemAfastamentoAnterior origem, int ano, int mes, int dia, int dias, string trecho)
    {
        var resultado = CalculadoraAfastamento.Calcular(TipoAfastamento.Doenca, Inicio, 30, 3000m, 3000m, false, 1621m, 8475.55m, new AfastamentoAnterior(origem, new DateOnly(ano, mes, dia), dias));
        Assert.Contains(trecho, resultado.Erro.Mensagem);
    }
}
