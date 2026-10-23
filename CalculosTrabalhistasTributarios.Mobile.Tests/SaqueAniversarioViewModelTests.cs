using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation;
using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class SaqueAniversarioViewModelTests
{
    [Theory]
    [InlineData("3.500", 1200)]
    [InlineData("3.500,00", 1200)]
    [InlineData("1.5", 0.75)]
    public async Task Usa_a_leitura_numerica_e_o_caso_de_uso_compartilhados(string saldo, decimal esperado)
    {
        var vm = Criar();
        vm.Saldo = saldo;
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(vm.Mensagem);
        Assert.NotNull(vm.Demonstrativo);
        Assert.Equal(esperado, vm.Demonstrativo.Resultado);
        Assert.NotEmpty(vm.Demonstrativo.Memoria);
    }

    [Fact]
    public async Task Preserva_a_composicao_do_saldo_e_o_repasse_ao_banco()
    {
        var vm = Criar();
        vm.Saldo = "9.000,00";
        vm.GarantiaBloqueada = "6.000,00";
        vm.MultaIncluida = "1.000,00";
        vm.SaldoIncluiGarantia = true;
        vm.ParcelaComprometida = "1.800,00";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.NotNull(vm.Demonstrativo);
        // Base 8.000: 20% + 650 = 2.250; repasse 1.800; disponível 450.
        Assert.Equal(2250m, vm.Demonstrativo.TotalProventos);
        Assert.Equal(1800m, vm.Demonstrativo.TotalDescontos);
        Assert.Equal(450m, vm.Demonstrativo.Resultado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    public async Task Recusa_texto_invalido_sem_converte_lo_em_zero(string saldo)
    {
        var vm = Criar();
        vm.Saldo = saldo;
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Contains("Saldo do extrato", vm.Mensagem);
        Assert.Null(vm.Demonstrativo);
    }

    [Theory]
    [InlineData("-1", 0)]
    [InlineData("0", 0)]
    [InlineData("1,001", 0)]
    [InlineData("1000000000001", 0)]
    [InlineData("8000", -1)]
    public async Task Exibe_as_falhas_de_validacao_do_caso_de_uso(string saldo, int mes)
    {
        var vm = Criar();
        vm.Saldo = saldo;
        vm.IndiceMes = mes;
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.False(string.IsNullOrWhiteSpace(vm.Mensagem));
        Assert.Null(vm.Demonstrativo);
    }

    [Fact]
    public async Task Editar_entrada_remove_resultado_anterior()
    {
        var vm = Criar();
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.NotNull(vm.Demonstrativo);
        vm.ParcelaComprometida = "100,00";
        Assert.Null(vm.Demonstrativo);
    }

    [Fact]
    public async Task Cancelamento_nao_vira_mensagem_de_validacao()
    {
        var vm = Criar(new SimulacaoCancelavel());
        var tarefa = vm.CalcularCommand.ExecuteAsync(null);
        Assert.True(vm.CalcularCommand.IsRunning);
        Assert.False(vm.CalcularCommand.CanExecute(null));
        vm.CalcularCommand.Cancel();
        await tarefa;
        Assert.Null(vm.Mensagem);
        Assert.Null(vm.Demonstrativo);
    }

    [Fact]
    public async Task Falha_inesperada_e_registrada_e_nao_vira_resultado_zero()
    {
        var logger = new LoggerDeTeste();
        var vm = new SaqueAniversarioViewModel(new SimulacaoComFalha(), logger, new CompartilhamentoDeTeste()) { Saldo = "8000", IndiceMes = 0 };
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.IsType<InvalidOperationException>(logger.Excecao);
        Assert.NotNull(vm.Mensagem);
        Assert.Null(vm.Demonstrativo);
    }

    [Fact]
    public void Composicao_valida_escopos_e_cria_formularios_independentes()
    {
        var servicos = new ServiceCollection().AddSaqueAniversario().AddSimulacaoTributaria().AddTrabalhistasMobile().AddPresentation();
        servicos.AddSingleton<ICompartilhamentoPdf, CompartilhamentoDeTeste>();
        servicos.AddSingleton<ILogger<SaqueAniversarioViewModel>>(NullLogger<SaqueAniversarioViewModel>.Instance);
        servicos.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        servicos.AddSingleton<ITributacaoConsulta>(new CalculosTrabalhistasTributarios.Infrastructure.Persistence.SqliteTributacaoConsulta(new CalculosTrabalhistasTributarios.Infrastructure.Persistence.BancoTributario("Data Source=:memory:")));
        servicos.AddSingleton<INavegacaoCalculadoras>(new NavegacaoDeTeste());
        using var provider = servicos.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        var primeiro = scope.ServiceProvider.GetRequiredService<SaqueAniversarioViewModel>();
        var segundo = scope.ServiceProvider.GetRequiredService<SaqueAniversarioViewModel>();
        primeiro.Saldo = "8000";
        Assert.NotSame(primeiro, segundo);
        Assert.Equal("0,00", segundo.Saldo);
        Assert.Equal(-1, segundo.IndiceMes);
        var referencias = typeof(SaqueAniversarioViewModel).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(referencias, r => r.Name is "CalculosTrabalhistasTributarios" or "CalculosTrabalhistasTributarios.Infrastructure"
            || (r.Name?.StartsWith("Microsoft.Maui", StringComparison.Ordinal) ?? false));
    }

    private static SaqueAniversarioViewModel Criar(ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest>? simular = null) =>
        new(simular ?? new SimularSaqueAniversarioUseCase(), NullLogger<SaqueAniversarioViewModel>.Instance, new CompartilhamentoDeTeste())
        { Saldo = "8.000,00", IndiceMes = 3 };

    private sealed class NavegacaoDeTeste : INavegacaoCalculadoras
    {
        public Task AbrirAsync(CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio.TipoCalculadoraMobile calculadora) => Task.CompletedTask;
    }
    private sealed class CompartilhamentoDeTeste : ICompartilhamentoPdf
    {
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SimulacaoCancelavel : ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest>
    {
        public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularSaqueAniversarioRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("A operação deveria ter sido cancelada.");
        }
    }

    private sealed class SimulacaoComFalha : ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest>
    {
        public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularSaqueAniversarioRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Falha simulada.");
    }

    private sealed class LoggerDeTeste : ILogger<SaqueAniversarioViewModel>
    {
        public Exception? Excecao { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Excecao = exception;
    }
}
