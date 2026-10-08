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

    [Fact]
    public async Task Informa_a_faixa_do_inss_e_do_irrf_com_e_sem_a_pensao()
    {
        // 8.500,00 passa do teto: o INSS de 988,07 termina na faixa de 14%. Com a pensão, a base do IRRF é
        // 8.500,00 - 988,07 - 2 x 189,59 - 2.111,99 = 5.020,76; sem ela, 7.132,75: as duas acima de 4.664,68, na faixa de 27,5%.
        var simulacao = await new SimularPensaoUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularPensaoRequest(new DateOnly(2026, 10, 1), 8500m, 8500m, 2, RegraPensao.PercentualDosLiquidos(30m), 0m), default).Sucesso();

        Assert.Equal(5020.76m, simulacao.Aplicada.Detalhes[^1].BaseIrrf);
        Assert.Equal(27.5m, simulacao.Aplicada.Detalhes[^1].Aliquota);
        Assert.Equal(27.5m, simulacao.AliquotaIrrfSemPensao);
        Assert.Equal("até a faixa de 14%", MemoriaCalculoPensao.FaixaInss(simulacao));
        Assert.Equal("Faixas aplicadas: INSS de R$ 988,07, até a faixa de 14%; IRRF com a pensão na faixa de 27,5% e sem a pensão na faixa de 27,5%.",
            Texto.Normalizar(MemoriaCalculoPensao.Faixas(simulacao, valor => valor.ToString("C2", new System.Globalization.CultureInfo("pt-BR")))));
    }

    [Fact]
    public async Task Faixa_isenta_do_irrf_e_inss_progressivo_de_2022()
    {
        // 06/2022: INSS de 1.212,00 x 7,5% + 788,00 x 9% = 90,90 + 70,92 = 161,82, na faixa de 9%. Bases do IRRF de
        // 2.000,00 - 161,82 - 200,00 (10% do bruto) = 1.638,18 e, sem a pensão, 1.838,18: ambas até 1.903,98, isentas.
        var simulacao = await new SimularPensaoUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularPensaoRequest(new DateOnly(2022, 6, 1), 2000m, 2000m, 0, new RegraPensao(BasePensao.RendimentosBrutos, 10m, 0m), 0m), default).Sucesso();

        Assert.Equal(161.82m, simulacao.ValorInss);
        Assert.Equal("até a faixa de 9%", MemoriaCalculoPensao.FaixaInss(simulacao));
        Assert.Equal(0m, simulacao.AliquotaIrrfSemPensao);
        Assert.Equal("faixa isenta", MemoriaCalculoPensao.FaixaIrrf(simulacao.Aplicada.Detalhes[^1].Aliquota, simulacao.Aplicada.Imposto));
    }

    [Fact]
    public async Task Antes_de_marco_de_2020_a_aliquota_do_inss_vale_para_toda_a_base()
    {
        // 06/2019: 2.500,00 está entre 1.751,82 e 2.919,72, faixa de 9% sobre toda a base: 225,00.
        var simulacao = await new SimularPensaoUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularPensaoRequest(new DateOnly(2019, 6, 1), 2500m, 2500m, 0, new RegraPensao(BasePensao.RendimentosBrutos, 10m, 0m), 0m), default).Sucesso();

        Assert.Equal(225.00m, simulacao.ValorInss);
        Assert.Equal("alíquota de 9% sobre toda a base", MemoriaCalculoPensao.FaixaInss(simulacao));
    }

    [Fact]
    public void Faixa_tributada_sem_imposto_a_reter_e_explicada()
    {
        Assert.Equal("faixa de 22,5%, sem IRRF a reter", MemoriaCalculoPensao.FaixaIrrf(22.5m, 0m));
        Assert.Equal("faixa de 7,5%", MemoriaCalculoPensao.FaixaIrrf(7.5m, 12.34m));
    }
}
