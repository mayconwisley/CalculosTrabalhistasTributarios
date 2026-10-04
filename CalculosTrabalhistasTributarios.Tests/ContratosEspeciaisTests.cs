using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Jovem aprendiz no custo do funcionário, estágio (Lei 11.788/2008) e trabalho intermitente (CLT, art. 452-A).</summary>
public class ContratosEspeciaisTests
{
    private static decimal Valor(IEnumerable<VerbaDto> verbas, string inicio) => verbas.Single(verba => verba.Descricao.StartsWith(inicio, StringComparison.Ordinal)).Valor;

    [Fact]
    public async Task Aprendiz_custa_fgts_de_2_por_cento()
    {
        var resultado = await new SimularCustoFuncionarioUseCase()
            .ExecutarAsync(new SimularCustoFuncionarioRequest(1_000m, RegimeTributario.SimplesNacional, 2m, 1m, 0m, 0m, false, Aprendiz: true), default).Sucesso();

        Assert.Equal(20m, Valor(resultado.Proventos, "FGTS do jovem aprendiz"));
        Assert.Equal(1_020m, resultado.Resultado);
    }

    private static async Task<DemonstrativoDto> EstagioAsync(string inicio, string fim, decimal gozados) =>
        await new SimularEstagioUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularEstagioRequest(new DateOnly(2026, 9, 1), 1_500m, 200m, DateOnly.Parse(inicio), DateOnly.Parse(fim), gozados, 0), default).Sucesso();

    [Fact]
    public async Task Estagio_de_um_ano_tem_30_dias_de_recesso_sem_inss()
    {
        var resultado = await EstagioAsync("2025-10-01", "2026-09-30", 10m);

        Assert.Equal(1_000.00m, Valor(resultado.Informativos, "Recesso"));
        Assert.Equal("20 dias", resultado.Destaques[1].Valor);
        Assert.DoesNotContain(resultado.Descontos, verba => verba.Descricao.StartsWith("INSS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Estagio_de_mais_de_2_anos_e_alertado()
    {
        var resultado = await EstagioAsync("2024-01-01", "2026-09-30", 0m);

        Assert.Contains(resultado.Observacoes, observacao => observacao.Contains("passou de 2 anos"));
    }

    [Fact]
    public async Task Intermitente_recebe_dsr_ferias_com_terco_e_13o_proporcional()
    {
        // 40 h x 20,00 = 800,00; DSR 800,00 ÷ 5 x 1 = 160,00; férias e 13º de 960,00 ÷ 12 = 80,00; 1/3 = 26,67.
        var resultado = await new SimularIntermitenteUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularIntermitenteRequest(new DateOnly(2026, 10, 1), 20m, 40m, 5, 1), default).Sucesso();

        Assert.Equal(160.00m, Valor(resultado.Proventos, "DSR"));
        Assert.Equal(26.67m, Valor(resultado.Proventos, "1/3"));
        Assert.Equal(1_146.67m, resultado.Resultado);
        Assert.Equal(83.20m, Valor(resultado.Informativos, "FGTS"));
    }
}
