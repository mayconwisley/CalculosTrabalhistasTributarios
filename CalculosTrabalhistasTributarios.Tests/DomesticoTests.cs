using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Empregado doméstico (LC 150/2015) e jovem aprendiz, e o DSR, os descontos e as verbas indenizatórias da rescisão.</summary>
public class DomesticoTests
{
    private static readonly DateOnly Outubro2026 = new(2026, 10, 1);

    private static decimal Valor(IEnumerable<VerbaDto> verbas, string inicio) => verbas.Single(verba => verba.Descricao.StartsWith(inicio, StringComparison.Ordinal)).Valor;

    private static async Task<Result<DemonstrativoDto>> RescisaoAsync(TipoVinculo vinculo, MotivoRescisao motivo, decimal salario = 2_000m, int semanasComFalta = 0, decimal outrosDescontos = 0m, decimal verbasIndenizatorias = 0m) =>
        await new SimularRescisaoUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularRescisaoRequest(
            new DateOnly(2024, 1, 1), new DateOnly(2026, 10, 31), motivo, CumprimentoAvisoPrevio.TrabalhadoOuDispensado, salario, 0m, 0, 0, 8_000m, 0m, 0,
            Vinculo: vinculo, SemanasComFalta: semanasComFalta, OutrosDescontos: outrosDescontos, VerbasIndenizatorias: verbasIndenizatorias), default);

    [Fact]
    public async Task Dae_soma_os_encargos_de_20_por_cento_e_as_retencoes()
    {
        var resultado = await new SimularDomesticoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularDomesticoRequest(Outubro2026, 2_000m, 0m, 0, 0, 0m), default).Sucesso();
        var tabelas = await (await Ambiente.ConsultaAsync()).ObterTabelasAsync(Outubro2026, default).Sucesso();
        var inss = tabelas.CalcularInss(2_000m).Valor;

        Assert.Equal(160.00m, Valor(resultado.Informativos, "DAE: contribuição patronal"));
        Assert.Equal(16.00m, Valor(resultado.Informativos, "DAE: GILRAT"));
        Assert.Equal(160.00m, Valor(resultado.Informativos, "DAE: FGTS"));
        Assert.Equal(64.00m, Valor(resultado.Informativos, "DAE: indenização compensatória"));
        Assert.Equal(400m + inss, Valor(resultado.Informativos, "Total do DAE"));
        Assert.Equal(2_000m - inss, resultado.Resultado);
        Assert.Contains("07/11/2026", resultado.Referencia);
    }

    [Theory]
    [InlineData(200, 120)]
    [InlineData(100, 100)]
    public async Task Vale_transporte_desconta_ate_6_por_cento_do_salario(decimal custo, decimal desconto)
    {
        var resultado = await new SimularDomesticoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularDomesticoRequest(Outubro2026, 2_000m, 0m, 0, 0, custo), default).Sucesso();

        Assert.Equal(desconto, Valor(resultado.Descontos, "Vale-transporte"));
    }

    [Fact]
    public async Task Custo_do_empregador_domestico_tem_20_por_cento_de_encargos()
    {
        var resultado = await new SimularCustoFuncionarioUseCase()
            .ExecutarAsync(new SimularCustoFuncionarioRequest(2_000m, RegimeTributario.EmpregadorDomestico, 2m, 1m, 5.8m, 0m, false), default).Sucesso();

        Assert.Equal(2_400.00m, resultado.Resultado);
        Assert.Equal(64.00m, Valor(resultado.Proventos, "Indenização compensatória"));
    }

    [Fact]
    public async Task Domestico_dispensado_saca_a_indenizacao_compensatoria_sem_multa_de_40()
    {
        // Base do mês: 2.000,00 de saldo + 1.666,67 de 13º (10/12) = 3.666,67; 3,2% = 117,33. Saldo estimado: 40% de 8.000,00.
        var resultado = (await RescisaoAsync(TipoVinculo.Domestico, MotivoRescisao.DispensaSemJustaCausa)).Sucesso();

        Assert.Equal(293.33m, Valor(resultado.Informativos, "Depósito do FGTS"));
        Assert.Equal(117.33m, Valor(resultado.Informativos, "Indenização compensatória do mês"));
        Assert.Equal(3_317.33m, Valor(resultado.Informativos, "Indenização compensatória para o empregado"));
        Assert.DoesNotContain(resultado.Informativos, verba => verba.Descricao.StartsWith("Multa rescisória", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Domestico_que_pede_demissao_deixa_a_indenizacao_com_o_empregador()
    {
        var resultado = (await RescisaoAsync(TipoVinculo.Domestico, MotivoRescisao.PedidoDeDemissao)).Sucesso();

        Assert.Equal(3_317.33m, Valor(resultado.Informativos, "Indenização compensatória que volta ao empregador"));
    }

    [Fact]
    public async Task Aprendiz_tem_fgts_de_2_por_cento()
    {
        var resultado = (await RescisaoAsync(TipoVinculo.Aprendiz, MotivoRescisao.TerminoDeContratoPorPrazo)).Sucesso();

        Assert.Equal(73.33m, Valor(resultado.Informativos, "Depósito do FGTS"));
    }

    [Fact]
    public async Task Rescisao_desconta_o_dsr_e_os_outros_descontos_e_paga_as_verbas_indenizatorias()
    {
        var resultado = (await RescisaoAsync(TipoVinculo.Empregado, MotivoRescisao.DispensaSemJustaCausa, salario: 3_000m, semanasComFalta: 2, outrosDescontos: 150m, verbasIndenizatorias: 500m)).Sucesso();

        Assert.Equal(200.00m, Valor(resultado.Descontos, "DSR perdido"));
        Assert.Equal(150m, Valor(resultado.Descontos, "Outros descontos"));
        Assert.Equal(500m, Valor(resultado.Proventos, "Verbas indenizatórias"));
    }

    [Fact]
    public async Task Outros_descontos_acima_de_uma_remuneracao_nao_sao_aceitos()
    {
        var erro = (await RescisaoAsync(TipoVinculo.Empregado, MotivoRescisao.DispensaSemJustaCausa, outrosDescontos: 2_500m)).Falha();

        Assert.Contains("art. 477, § 5º", erro.Mensagem);
    }

    [Theory]
    [InlineData(15, 3)]
    [InlineData(14, 0)]
    public async Task Seguro_desemprego_do_domestico_e_de_um_salario_minimo(int meses, int parcelas)
    {
        var resultado = await new SimularSeguroDesempregoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularSeguroDesempregoRequest(new DateOnly(2026, 10, 15), [], SolicitacaoSeguroDesemprego.Primeira, meses, Domestico: true), default).Sucesso();

        Assert.Equal(1_621m * parcelas, resultado.Proventos.Sum(verba => verba.Valor));
    }
}
