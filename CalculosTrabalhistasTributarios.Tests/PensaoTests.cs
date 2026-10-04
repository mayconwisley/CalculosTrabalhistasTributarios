using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class PensaoTests
{
    [Theory]
    [InlineData("2025-10")]
    [InlineData("2026-10")]
    [InlineData("2022-06")]
    public async Task Pensao_sobre_o_liquido_fecha_no_ponto_fixo_exato(string mes)
    {
        var competencia = DateOnly.ParseExact(mes + "-01", "yyyy-MM-dd");
        var pensao = new SimularPensaoUseCase(await Ambiente.ConsultaAsync());
        var falhas = new List<string>();
        for (var bruto = 2500m; bruto <= 27_000m; bruto += 137.37m)
            foreach (var percentual in new[] { 10m, 20m, 30m, 33.33m, 50m })
                foreach (var dependentes in new[] { 0, 2 })
                {
                    var simulacao = await pensao.ExecutarAsync(new SimularPensaoRequest(competencia, bruto, bruto, dependentes, RegraPensao.PercentualDosLiquidos(percentual), 0m), default).Sucesso();
                    var normal = simulacao.Normal;
                    // A pensão é o percentual do líquido com o IRRF apurado com essa mesma pensão deduzida.
                    var esperada = Math.Round((bruto - simulacao.ValorInss - normal.Imposto) * percentual / 100m, 2, MidpointRounding.AwayFromZero);
                    if (normal.Detalhes[^1].PensaoDeduzida != normal.Pensao || esperada != normal.Pensao)
                        falhas.Add($"{bruto} {percentual}% {dependentes} dep.: pensão {normal.Pensao}, deduzida {normal.Detalhes[^1].PensaoDeduzida}, esperada {esperada}");
                }
        Assert.Empty(falhas.Take(10));
    }

    [Fact]
    public async Task Calculadora_de_pensao_e_13o_dao_o_mesmo_resultado()
    {
        var consulta = await Ambiente.ConsultaAsync();
        var pensao = await new SimularPensaoUseCase(consulta).ExecutarAsync(new SimularPensaoRequest(new DateOnly(2026, 10, 1), 8500m, 8500m, 2, RegraPensao.PercentualDosLiquidos(30m), 0m), default).Sucesso();
        var decimo = await new SimularDecimoTerceiroUseCase(consulta).ExecutarAsync(new SimularDecimoTerceiroRequest(new DateOnly(2026, 12, 1), 8500m, 0m, 12, 2, AdiantamentoDecimoTerceiro.SemAdiantamento, 0m, RegraPensao.PercentualDosLiquidos(30m)), default).Sucesso();
        Assert.Equal(988.07m, pensao.ValorInss);
        Assert.Equal(471.98m, pensao.Aplicada.Imposto);
        Assert.Equal(2111.99m, pensao.Aplicada.Pensao);
        Assert.Equal(1052.78m, pensao.ImpostoSemPensao);
        Assert.Equal(pensao.Aplicada.Imposto, decimo.Descontos.Single(verba => verba.Descricao.StartsWith("IRRF")).Valor);
        Assert.Equal(pensao.Aplicada.Pensao, decimo.Descontos.Single(verba => verba.Descricao.StartsWith("Pensão")).Valor);
    }

    [Fact]
    public async Task Antes_de_maio_de_2023_so_existe_a_modalidade_normal()
    {
        var simulacao = await new SimularPensaoUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularPensaoRequest(new DateOnly(2022, 6, 1), 5000m, 5000m, 0, RegraPensao.PercentualDosLiquidos(30m), 0m), default).Sucesso();
        Assert.False(simulacao.SimplificadoDisponivel);
        Assert.Single(simulacao.Modalidades);
        Assert.Same(simulacao.Normal, simulacao.Aplicada);
    }

    [Fact]
    public async Task Soma_dos_percentuais_na_mesma_base_nao_passa_de_100()
    {
        var pensao = new SimularPensaoUseCase(await Ambiente.ConsultaAsync());
        BeneficiarioPensao Beneficiario(string nome, decimal percentual) => new(nome, RegraPensao.PercentualDosLiquidos(percentual));
        var erro = await pensao.ExecutarAsync(new SimularPensaoRequest(new DateOnly(2026, 10, 1), 8500m, 8500m, 2, [Beneficiario("A", 40m), Beneficiario("B", 40m), Beneficiario("C", 40m)], 0m), default).Falha();
        Assert.Contains("passa de 100%", erro.Mensagem);
        // Na ordem sucessiva, cada pensão incide sobre o que sobra das anteriores.
        await pensao.ExecutarAsync(new SimularPensaoRequest(new DateOnly(2026, 10, 1), 8500m, 8500m, 2, [Beneficiario("A", 60m), Beneficiario("B", 60m)], 0m, Sucessiva: true), default).Sucesso();
    }

    [Fact]
    public async Task Percentual_do_salario_minimo_pode_passar_de_100()
    {
        var simulacao = await new SimularPensaoUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularPensaoRequest(new DateOnly(2026, 10, 1), 8500m, 8500m, 2, new RegraPensao(BasePensao.SalarioMinimo, 150m, 0m), 0m), default).Sucesso();
        Assert.Equal(2431.50m, simulacao.Aplicada.Pensao);
        new RegraPensao(BasePensao.RendimentosLiquidos, 150m, 0m).Validar().Falha();
        new RegraPensao(BasePensao.SalarioMinimo, 1001m, 0m).Validar().Falha();
    }

    [Fact]
    public async Task Irrf_de_ate_dez_reais_nao_e_retido_na_pensao()
    {
        var simulacao = await new SimularPensaoUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularPensaoRequest(new DateOnly(2025, 6, 1), 3150m, 3150m, 0, RegraPensao.PercentualDosLiquidos(1m), 0m), default).Sucesso();
        // O simplificado calcula R$ 8,55, que não é retido; a fonte aplica a modalidade de menor IRRF.
        Assert.Equal(0m, simulacao.Simplificada.Imposto);
        Assert.Same(simulacao.Simplificada, simulacao.Aplicada);
        Assert.Equal(0m, simulacao.ImpostoSemPensao);
    }

    [Fact]
    public async Task Revisao_compara_a_pensao_atual_com_a_proposta()
    {
        var consulta = await Ambiente.ConsultaAsync();
        var revisao = await new SimularRevisaoPensaoUseCase(new SimularPensaoUseCase(consulta), consulta)
            .ExecutarAsync(new SimularRevisaoPensaoRequest(new DateOnly(2026, 10, 1), 8500m, 2, RegraPensao.PercentualDosLiquidos(30m), new RegraPensao(BasePensao.SalarioMinimo, 150m, 0m)), default).Sucesso();
        Assert.Equal("R$ 2.111,99", Texto.Normalizar(revisao.Destaques[0].Valor));
        Assert.Equal("R$ 2.431,50", Texto.Normalizar(revisao.Destaques[1].Valor));
        Assert.Equal("+R$ 319,51", Texto.Normalizar(revisao.Destaques[2].Valor));
    }
}
