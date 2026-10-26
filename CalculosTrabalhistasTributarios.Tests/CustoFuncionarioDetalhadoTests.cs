using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.CustoFuncionario;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Motor = CalculosTrabalhistasTributarios.Domain.Trabalhista.CustoFuncionario.CalculadoraCustoFuncionario;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class CustoFuncionarioDetalhadoTests
{
    private static EntradaCustoFuncionario Padrao => new(3000m, RegimeTributario.LucroRealOuPresumido, 2m, 1m, 5.8m, 500m, true);

    [Fact]
    public void Concilia_desembolso_reserva_anual_media_e_hora()
    {
        var r = Motor.Calcular(Padrao).Valor;
        // Folha: 3000 + 600 + 60 + 174 + 240 = 4074; benefícios: 500.
        // Reserva: 250 + 333,33 + 208,83 = 792,16. Ano: 5366,16 × 12 − 4074.
        Assert.Equal(4574m, r.Recorrente);
        Assert.Equal(792.16m, r.Reserva);
        Assert.Equal(5366.16m, r.TotalComReserva);
        Assert.Equal(60319.92m, r.CustoAnual);
        Assert.Equal(5026.66m, r.MediaMensal);
        Assert.Equal(5026.66m / 220m, r.CustoHora);
        Assert.Equal(r.Recorrente + r.Reserva, r.TotalComReserva);
    }

    [Theory]
    [InlineData(RegimeTributario.LucroRealOuPresumido, 4074)]
    [InlineData(RegimeTributario.SimplesNacional, 3240)]
    [InlineData(RegimeTributario.SimplesNacionalAnexoIV, 3900)]
    [InlineData(RegimeTributario.EmpregadorDomestico, 3600)]
    public void Regimes_sem_provisao_conciliam_ano(RegimeTributario regime, decimal esperado)
    {
        var r = Motor.Calcular(Padrao with { Regime = regime, Beneficios = 0m, IncluirProvisoes = false }).Valor;
        Assert.Equal(esperado, r.Recorrente);
        Assert.Equal(0m, r.Reserva);
        Assert.Equal(esperado * 12m, r.CustoAnual);
        Assert.Equal(esperado, r.MediaMensal);
    }

    [Fact]
    public void Aprendiz_no_simples_recolhe_dois_por_cento()
    {
        var r = Motor.Calcular(Padrao with { Regime = RegimeTributario.SimplesNacional, Aprendiz = true }).Valor;
        Assert.Equal(60m, r.Fgts);
        Assert.Equal(2m, r.AliquotaEncargos);
    }

    [Fact]
    public void Beneficios_sao_liquidos_da_participacao_sem_reduzir_salario()
    {
        var r = Motor.Calcular(Padrao with { Beneficios = 100m, BeneficiosDetalhados = [new("Alimentação", 600m, 120m), new("Saúde", 200m, 50m)] }).Valor;
        Assert.Equal(730m, r.Beneficios);
        Assert.Equal(4804m, r.Recorrente);
    }

    [Theory]
    [InlineData(180)]
    [InlineData(200)]
    [InlineData(220)]
    [InlineData(744)]
    public void Divisor_configuravel_nao_altera_custo_anual(decimal horas)
    {
        var r = Motor.Calcular(Padrao with { HorasMensais = horas }).Valor;
        Assert.Equal(60319.92m, r.CustoAnual);
        Assert.Equal(5026.66m / horas, r.CustoHora);
    }

    public static IEnumerable<object[]> Invalidos()
    {
        yield return [Padrao with { Regime = (RegimeTributario)999 }];
        yield return [Padrao with { Regime = RegimeTributario.EmpregadorDomestico, Aprendiz = true }];
        yield return [Padrao with { Salario = -0.01m }];
        yield return [Padrao with { Salario = 0m }];
        yield return [Padrao with { Salario = decimal.MaxValue }];
        yield return [Padrao with { Salario = 0.001m }];
        yield return [Padrao with { Beneficios = decimal.MaxValue }];
        yield return [Padrao with { Terceiros = 100.0001m }];
        yield return [Padrao with { Rat = 1.5m }];
        yield return [Padrao with { Fap = 0.4999m }];
        yield return [Padrao with { Fap = 2.0001m }];
        yield return [Padrao with { Fap = 1.00001m }];
        yield return [Padrao with { HorasMensais = 0m }];
        yield return [Padrao with { HorasMensais = 744.01m }];
        yield return [Padrao with { BeneficiosDetalhados = [new("", 1m, 0m)] }];
        yield return [Padrao with { BeneficiosDetalhados = [new("Vale", 10m, 11m)] }];
        yield return [Padrao with { BeneficiosDetalhados = [new("Vale", 10m, -1m)] }];
        yield return [Padrao with { BeneficiosDetalhados = Enumerable.Repeat(new BeneficioFuncionario("Vale", 1m, 0m), 31).ToArray() }];
    }

    [Theory]
    [MemberData(nameof(Invalidos))]
    public void Rejeita_entrada_invalida_sem_excecao(EntradaCustoFuncionario entrada)
    {
        var r = Motor.Calcular(entrada);
        Assert.True(r.Falhou);
        Assert.NotEmpty(r.Erro.Mensagem);
    }

    [Fact]
    public void Aceita_limites_e_ignora_parametros_inaplicaveis()
    {
        Assert.True(Motor.Calcular(Padrao with { Salario = 100_000_000m, Fap = 0.5m, Terceiros = 100m }).Sucesso);
        Assert.True(Motor.Calcular(Padrao with { Fap = 2m }).Sucesso);
        Assert.True(Motor.Calcular(Padrao with { Regime = RegimeTributario.SimplesNacional, Fap = decimal.MaxValue, Rat = decimal.MaxValue, Terceiros = decimal.MaxValue }).Sucesso);
    }

    [Fact]
    public async Task Historico_antigo_e_novo_preservam_formulario_e_resultado()
    {
        var c = new Presentation.ViewModels.Calculadoras.CalculadoraCustoFuncionario(new SimularCustoFuncionarioUseCase());
        c.ImportarCampos(new Dictionary<string, string> { ["Salário"] = "3.000,00", ["Benefícios"] = "500,00" });
        Assert.Equal("220", c.ExportarCampos()["Divisor mensal (horas)"]);
        var campo = c.Campos.OfType<CampoBeneficiosFuncionarioViewModel>().Single();
        campo.AdicionarCommand.Execute(null);
        campo.Linhas[0].Descricao.Valor = "Alimentação";
        campo.Linhas[0].Custo.Valor = "600,00";
        campo.Linhas[0].Participacao.Valor = "120,00";
        var salvo = c.ExportarCampos();
        var outro = new Presentation.ViewModels.Calculadoras.CalculadoraCustoFuncionario(new SimularCustoFuncionarioUseCase());
        Assert.True(outro.TentarImportarCampos(salvo).Sucesso);
        Assert.Equal(salvo.OrderBy(x => x.Key), outro.ExportarCampos().OrderBy(x => x.Key));
        var antes = await c.CalcularAsync(TestContext.Current.CancellationToken);
        var depois = await outro.CalcularAsync(TestContext.Current.CancellationToken);
        Assert.Equal(antes.Valor.Resultado, depois.Valor.Resultado);
        Assert.Equal(5846.16m, depois.Valor.Resultado);
        Assert.Contains(depois.Valor.Proventos, v => v.Descricao == "Benefício: Alimentação" && v.Valor == 480m);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    public void Historico_invalido_nao_altera_formulario(string json)
    {
        var c = new Presentation.ViewModels.Calculadoras.CalculadoraCustoFuncionario(new SimularCustoFuncionarioUseCase());
        var antes = c.ExportarCampos();
        var r = c.TentarImportarCampos(new Dictionary<string, string> { ["Salário"] = "999", ["Benefícios detalhados"] = json });
        Assert.True(r.Falhou);
        Assert.Equal(antes.OrderBy(x => x.Key), c.ExportarCampos().OrderBy(x => x.Key));
    }

    [Fact]
    public async Task Respeita_cancelamento_antes_da_apuracao()
    {
        using var fonte = new CancellationTokenSource();
        fonte.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SimularCustoFuncionarioUseCase().ExecutarAsync(new(3000m, RegimeTributario.SimplesNacional, 2m, 1m, 0m, 0m, true), fonte.Token));
    }
}
