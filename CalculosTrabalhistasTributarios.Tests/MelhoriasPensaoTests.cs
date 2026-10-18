using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Infrastructure;
using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class MelhoriasPensaoTests
{
    [Fact]
    public void Motor_preserva_ordem_e_base_propria()
    {
        RegraPensao[] regras = [new(BasePensao.ValorFixo, 0m, 100m), new(BasePensao.RendimentosLiquidos, 20m, 0m), new(BasePensao.SalarioMinimo, 50m, 0m)];
        var parcelas = MotorPensoes.Calcular(regras, true, 5000m, 500m, 300m, 1600m);
        Assert.Equal(100m, parcelas[0].Valor);
        Assert.Equal(4100m, parcelas[1].Base);
        Assert.Equal(820m, parcelas[1].Valor);
        Assert.Equal(800m, parcelas[2].Valor);
    }

    [Fact]
    public void Calculo_oscilante_nao_retorna_sucesso()
    {
        var resultado = CalculadoraPensao.Calcular(RegraPensao.PercentualDosLiquidos(100m), 100m, 0m,
            pensao => pensao, imposto => imposto);
        Assert.True(resultado.Falhou);
        Assert.Contains("não estabilizou", resultado.Erro.Mensagem);
    }

    [Theory]
    [InlineData("1000000000")]
    [InlineData("0.001")]
    [InlineData("-1")]
    public async Task Rejeita_valores_fora_dos_limites(string texto)
    {
        var valor = decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
        var resultado = await new SimularPensaoUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(
            new(new DateOnly(2026, 10, 1), valor, 0m, 0, RegraPensao.PercentualDosLiquidos(30m), 0m), default);
        Assert.True(resultado.Falhou);
        Assert.Contains("Valor bruto", resultado.Erro.Mensagem);
    }

    [Fact]
    public void Rejeita_enum_invalido_e_precisao_excessiva()
    {
        Assert.True(new RegraPensao((BasePensao)99, 10m, 0m).Validar().Falhou);
        Assert.True(new RegraPensao(BasePensao.RendimentosLiquidos, 30.001m, 0m).Validar().Falhou);
        Assert.True(new RegraPensao(BasePensao.ValorFixo, 0m, 10.001m).Validar().Falhou);
        Assert.True(LimitesPensao.MoedaValida(LimitesPensao.MaximoMonetario));
    }

    [Fact]
    public async Task Edicao_bloqueia_exportacao_e_recalculo_reabilita()
    {
        using var servicos = await CriarServicos();
        var vm = servicos.GetRequiredService<IPensaoViewModelFactory>().Criar();
        Preencher(vm);
        await vm.RecalcularAsync();
        Assert.True(vm.ExportarPdfCommand.CanExecute(null));
        vm.Beneficiarios[0].Percentual = "25,00";
        Assert.True(vm.ResultadoDesatualizado);
        Assert.False(vm.ExportarPdfCommand.CanExecute(null));
        Assert.False(vm.ExportarExcelCommand.CanExecute(null));
        await vm.RecalcularAsync();
        Assert.False(vm.ResultadoDesatualizado);
        Assert.True(vm.ExportarExcelCommand.CanExecute(null));
        vm.ExibirMemoriaDetalhada = true;
        Assert.True(vm.TemMemoria);
        Assert.False(vm.ResultadoDesatualizado);
        Assert.Contains(vm.ComparativoPensao, item => item.Modalidade.Contains("aplicada"));
    }

    [Fact]
    public async Task Base_manual_e_historico_antigo_sao_preservados()
    {
        using var servicos = await CriarServicos();
        var vm = servicos.GetRequiredService<IPensaoViewModelFactory>().Criar();
        Preencher(vm);
        vm.UsarBrutoComoBaseInss = false;
        vm.BaseInss = "4.000,00";
        vm.ValorBruto = "9.000,00";
        Assert.Equal("4.000,00", vm.BaseInss);
        var dados = vm.ExportarDados();
        dados.Campos.Remove(nameof(vm.UsarBrutoComoBaseInss));
        var outra = servicos.GetRequiredService<IPensaoViewModelFactory>().Criar();
        outra.ImportarDados(dados);
        Assert.False(outra.UsarBrutoComoBaseInss);
        outra.ValorBruto = "10.000,00";
        Assert.Equal("4.000,00", outra.BaseInss);
        outra.UsarBrutoComoBaseInss = true;
        Assert.Equal(outra.ValorBruto, outra.BaseInss);
    }

    [Fact]
    public async Task Reordenacao_preserva_campos_e_ordem_no_historico()
    {
        using var servicos = await CriarServicos();
        var vm = servicos.GetRequiredService<IPensaoViewModelFactory>().Criar();
        Preencher(vm);
        vm.AdicionarBeneficiarioCommand.Execute(null);
        var segundo = vm.Beneficiarios[1];
        segundo.Nome = "Segundo";
        segundo.Percentual = "10,00";
        Assert.False(vm.DescerBeneficiarioCommand.CanExecute(segundo));
        vm.SubirBeneficiarioCommand.Execute(segundo);
        Assert.Same(segundo, vm.Beneficiarios[0]);
        Assert.False(vm.SubirBeneficiarioCommand.CanExecute(segundo));
        var outra = servicos.GetRequiredService<IPensaoViewModelFactory>().Criar();
        outra.ImportarDados(vm.ExportarDados());
        Assert.Equal("Segundo", outra.Beneficiarios[0].Nome);
        Assert.Equal("10,00", outra.Beneficiarios[0].Percentual);
    }

    [Theory]
    [InlineData("base")]
    [InlineData("ordem")]
    [InlineData("sem-linhas")]
    [InlineData("sem-valor")]
    [InlineData("sem-campo")]
    [InlineData("opcao-invalida")]
    public async Task Importacao_invalida_preserva_formulario_e_resultado(string problema)
    {
        using var servicos = await CriarServicos();
        var vm = servicos.GetRequiredService<IPensaoViewModelFactory>().Criar();
        Preencher(vm);
        await vm.RecalcularAsync();
        var anterior = vm.ExportarDados().ParaJson();
        var dados = vm.ExportarDados();
        dados.Campos[nameof(vm.ValorBruto)] = "1.000,00";
        switch (problema)
        {
            case "base": dados.Linhas![0]["BaseSelecionada"] = "Base desconhecida"; break;
            case "ordem": dados.Campos[nameof(vm.OrdemSelecionada)] = "Ordem desconhecida"; break;
            case "sem-linhas": dados = dados with { Linhas = [] }; break;
            case "sem-valor": dados.Linhas![0].Remove("Percentual"); break;
            case "sem-campo": dados.Campos.Remove(nameof(vm.Competencia)); break;
            case "opcao-invalida": dados.Campos[nameof(vm.UsarBrutoComoBaseInss)] = "Talvez"; break;
        }
        var resultado = vm.TentarImportarDados(dados);
        Assert.True(resultado.Falhou);
        Assert.Equal(anterior, vm.ExportarDados().ParaJson());
        Assert.False(vm.ResultadoDesatualizado);
        Assert.True(vm.ExportarExcelCommand.CanExecute(null));
    }

    [Fact]
    public async Task Importacao_valida_substitui_beneficiarios_e_preserva_textos_invalidos_para_correcao()
    {
        using var servicos = await CriarServicos();
        var vm = servicos.GetRequiredService<IPensaoViewModelFactory>().Criar();
        Preencher(vm);
        var dados = vm.ExportarDados();
        dados.Linhas![0]["Nome"] = "Importado";
        dados.Linhas[0]["Percentual"] = "30,001";
        vm.AdicionarBeneficiarioCommand.Execute(null);
        Assert.True(vm.TentarImportarDados(dados).Sucesso);
        Assert.Single(vm.Beneficiarios);
        Assert.Equal("Importado", vm.Beneficiarios[0].Nome);
        Assert.Equal("30,001", vm.Beneficiarios[0].Percentual);
        Assert.NotEmpty(vm.Beneficiarios[0]["Percentual"]);
    }

    [Theory]
    [InlineData("base-invalida")]
    [InlineData("json-invalido")]
    [InlineData("null")]
    public async Task Historico_rejeitado_nao_sincroniza_nem_recalcula(string problema)
    {
        var notificador = new NotificadorColetor();
        using var servicos = await CriarServicos(notificador);
        var vm = servicos.GetRequiredService<IPensaoViewModelFactory>().Criar();
        Preencher(vm);
        var anterior = vm.ExportarDados().ParaJson();
        var dados = vm.ExportarDados();
        dados.Linhas![0]["BaseSelecionada"] = "Desconhecida";
        var json = problema == "base-invalida" ? dados.ParaJson() : problema == "null" ? "null" : "{invalido";
        var sincronizou = false;
        vm.Historico.Sincronizado += () => sincronizou = true;
        await vm.Historico.CarregarAsync(new(123, "Pensao", "Pensão", "Inválido", json, DateTime.Now, DateTime.Now));
        Assert.False(sincronizou);
        Assert.False(vm.TemResultado);
        Assert.Equal("", vm.Historico.Situacao);
        Assert.Equal(anterior, vm.ExportarDados().ParaJson());
        Assert.Single(notificador.Mensagens);
    }

    [Theory]
    [InlineData("10/2026", "10.000,00", "8.475,55", true)]
    [InlineData("10/2026", "8.475,55", "8.475,55", false)]
    [InlineData("10/2026", "5.000,00", "5.000,00", false)]
    [InlineData("10/2026", "0,00", "0,00", false)]
    [InlineData("06/2019", "10.000,00", "5.839,45", true)]
    public async Task Cartao_inss_informa_base_efetivamente_utilizada(string competencia, string baseInformada, string baseUtilizada, bool limitada)
    {
        using var servicos = await CriarServicos();
        var vm = servicos.GetRequiredService<IPensaoViewModelFactory>().Criar();
        Preencher(vm);
        vm.Competencia = competencia;
        vm.ValorBruto = baseInformada;
        await vm.RecalcularAsync();
        var inss = vm.IndicadoresResumo.Single(item => item.Rotulo == "INSS");
        var complemento = inss.Complemento ?? "";
        Assert.Contains($"Base utilizada: R$ {baseUtilizada}", Texto.Normalizar(complemento));
        if (limitada)
        {
            Assert.Contains("limitada ao teto", complemento);
            Assert.Contains($"base informada: R$ {baseInformada}", Texto.Normalizar(complemento));
        }
        else
        {
            Assert.DoesNotContain("limitada ao teto", complemento);
            Assert.DoesNotContain("base informada", complemento);
        }
        if (competencia == "10/2026" && limitada)
            Assert.Equal("R$ 988,07", Texto.Normalizar(inss.Valor));
    }

    private static void Preencher(PensaoViewModel vm)
    {
        vm.Competencia = "10/2026";
        vm.ValorBruto = "8.500,00";
        vm.Dependentes = "2";
        vm.Beneficiarios[0].Percentual = "30,00";
    }

    private static async Task<ServiceProvider> CriarServicos(IUserNotifier? notificador = null) => new ServiceCollection()
        .AddApplication().AddInfrastructure().AddPresentation()
        .AddSingleton<ITributacaoConsulta>(await Ambiente.ConsultaAsync())
        .AddSingleton<IUserNotifier>(notificador ?? new Notificador()).BuildServiceProvider();

    private sealed class NotificadorColetor : IUserNotifier
    {
        public List<string> Mensagens { get; } = [];
        public void MostrarAviso(string mensagem, string titulo = "Dados inválidos") => Mensagens.Add(mensagem);
        public void MostrarErro(string mensagem, Exception exception) => throw new InvalidOperationException(mensagem, exception);
        public void MostrarFalha(Erro erro, string contexto) => Mensagens.Add(erro.Mensagem);
        public bool Confirmar(string mensagem, string titulo) => true;
    }

    private sealed class Notificador : IUserNotifier
    {
        public void MostrarAviso(string mensagem, string titulo = "Dados inválidos") => throw new InvalidOperationException(mensagem);
        public void MostrarErro(string mensagem, Exception exception) => throw new InvalidOperationException(mensagem, exception);
        public void MostrarFalha(Erro erro, string contexto) => throw new InvalidOperationException(erro.Mensagem);
        public bool Confirmar(string mensagem, string titulo) => true;
    }
}
