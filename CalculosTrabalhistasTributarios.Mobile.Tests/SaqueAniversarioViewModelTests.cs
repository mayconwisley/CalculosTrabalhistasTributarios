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
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Abertura;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class SaqueAniversarioViewModelTests
{
    [Fact]
    public async Task Analise_de_nova_antecipacao_e_historico_preservam_confirmacoes()
    {
        var vm = Criar();
        vm.Saldo = "18.000,00";
        vm.GarantiaBloqueada = "10.000,00";
        vm.SaldoIncluiGarantia = true;
        vm.DataAnalise = "07/10/2026";
        vm.IndiceCarencia = 1;
        vm.IndiceContratoNovo = 1;
        vm.IndiceProximoCedido = 2;
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal("Nova antecipação", vm.Demonstrativo?.Destaques[0].Rotulo);
        Assert.Equal("Sujeita à análise do banco", vm.Demonstrativo?.Destaques[0].Valor);

        var copia = Criar();
        Assert.True(copia.TentarImportarDados(vm.ExportarDados()).Sucesso);
        Assert.Equal("07/10/2026", copia.DataAnalise);
        Assert.Equal(1, copia.IndiceContratoNovo);
        Assert.Equal(2, copia.IndiceProximoCedido);
    }

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
        var servicos = new ServiceCollection().AddCalculadoras().AddPresentation();
        servicos.AddSingleton<ICompartilhamentoPdf, CompartilhamentoDeTeste>();
        servicos.AddSingleton<ILogger<SaqueAniversarioViewModel>>(NullLogger<SaqueAniversarioViewModel>.Instance);
        servicos.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        servicos.AddSingleton<ITributacaoConsulta>(new CalculosTrabalhistasTributarios.Infrastructure.Persistence.SqliteTributacaoConsulta(new CalculosTrabalhistasTributarios.Infrastructure.Persistence.BancoTributario("Data Source=:memory:")));
        servicos.AddSingleton<CalculosTrabalhistasTributarios.Application.Interfaces.IIndicesEconomicos>(new CalculosTrabalhistasTributarios.Infrastructure.Persistence.SqliteIndicesEconomicos(new CalculosTrabalhistasTributarios.Infrastructure.Persistence.BancoTributario("Data Source=:memory:")));
        servicos.AddSingleton<INavegacaoCalculadoras>(new NavegacaoDeTeste());
        servicos.AddSingleton<INavegacaoMenu>(new MenuDeTeste());
        servicos.AddSingleton<IPreferenciasMobile>(new PreferenciasDeTeste());
        servicos.AddSingleton<IPlataformaMobile>(new PlataformaDeTeste());
        servicos.AddSingleton<IAtualizarTodasTabelasUseCase>(new AtualizacaoDeTeste());
        using var provider = servicos.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        var primeiro = scope.ServiceProvider.GetRequiredService<SaqueAniversarioViewModel>();
        var segundo = scope.ServiceProvider.GetRequiredService<SaqueAniversarioViewModel>();
        primeiro.Saldo = "8000";
        Assert.NotSame(primeiro, segundo);
        Assert.Equal("0,00", segundo.Saldo);
        Assert.Equal(-1, segundo.IndiceMes);
        foreach (var cartao in CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio.CatalogoCalculadorasMobile.Cartoes.Where(cartao => cartao.ViewModel is not null))
            Assert.NotSame(scope.ServiceProvider.GetRequiredService(cartao.ViewModel!), scope.ServiceProvider.GetRequiredService(cartao.ViewModel!));
        foreach (var cartao in CatalogoCalculadorasMobile.Cartoes.Where(cartao => cartao.ViewModel is not null))
        {
            var origem = Assert.IsAssignableFrom<IFormularioHistoricoMobile>(scope.ServiceProvider.GetRequiredService(cartao.ViewModel!));
            var destino = Assert.IsAssignableFrom<IFormularioHistoricoMobile>(scope.ServiceProvider.GetRequiredService(cartao.ViewModel!));
            var dados = origem.ExportarDados();
            Assert.True(destino.TentarImportarDados(dados).Sucesso, cartao.Titulo);
            Assert.Equal(JsonSerializer.Serialize(dados), JsonSerializer.Serialize(destino.ExportarDados()));
        }
        var referencias = typeof(SaqueAniversarioViewModel).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(referencias, r => r.Name is "CalculosTrabalhistasTributarios" or "CalculosTrabalhistasTributarios.Infrastructure"
            || (r.Name?.StartsWith("Microsoft.Maui", StringComparison.Ordinal) ?? false));
    }

    [Fact]
    public void Busca_do_catalogo_encontra_calculadoras_e_tabelas_sem_acentos_e_mantem_favoritas()
    {
        var preferencias = new PreferenciasDeTeste();
        var vm = new InicioViewModel(new NavegacaoDeTeste(), new MenuDeTeste(), preferencias, new PlataformaDeTeste(),
            new SituacaoAtualizacaoTabelas(), NullLogger<InicioViewModel>.Instance);
        Assert.Equal(50, vm.QuantidadeCalculadoras);
        Assert.Equal(16, vm.QuantidadeTabelas);
        vm.Busca = "salario familia";
        var calculadora = Assert.Single(vm.Grupos.SelectMany(grupo => grupo.Atalhos), atalho => atalho.Titulo == "Salário-família");
        Assert.Contains(vm.GruposTabelas.SelectMany(grupo => grupo.Atalhos), atalho => atalho.Titulo == "Salário-família");
        vm.AlternarFavoritoCommand.Execute(calculadora);
        Assert.True(calculadora.Favorito);
        Assert.Contains(calculadora, vm.Favoritas.Atalhos);
        Assert.DoesNotContain(calculadora, vm.Grupos.SelectMany(grupo => grupo.Atalhos));
        vm.Busca = "nenhum cálculo com estas palavras";
        Assert.True(vm.SemResultado);
        vm.LimparBuscaCommand.Execute(null);
        Assert.False(vm.SemResultado);
    }

    [Fact]
    public void Atualizacao_ao_abrir_respeita_preferencia_e_dia_da_ultima_consulta()
    {
        var agora = new DateTime(2026, 10, 23, 10, 0, 0);
        Assert.True(AtualizacaoTabelasViewModel.DeveAtualizar(true, null, agora));
        Assert.False(AtualizacaoTabelasViewModel.DeveAtualizar(false, null, agora));
        Assert.False(AtualizacaoTabelasViewModel.DeveAtualizar(true, agora.AddHours(-2), agora));
        Assert.True(AtualizacaoTabelasViewModel.DeveAtualizar(true, agora.AddDays(-1), agora));
    }

    private static SaqueAniversarioViewModel Criar(ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest>? simular = null) =>
        new(simular ?? new SimularSaqueAniversarioUseCase(), NullLogger<SaqueAniversarioViewModel>.Instance, new CompartilhamentoDeTeste())
        { Saldo = "8.000,00", IndiceMes = 3 };

    private sealed class NavegacaoDeTeste : INavegacaoCalculadoras
    {
        public Task AbrirAsync(CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio.TipoCalculadoraMobile calculadora) => Task.CompletedTask;
        public Task AbrirAsync(CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio.TipoCalculadoraMobile calculadora,
            CalculosTrabalhistasTributarios.Domain.Trabalhista.QuitacaoBancoHoras quitacao) => Task.CompletedTask;
    }
    private sealed class MenuDeTeste : INavegacaoMenu
    {
        public Task AbrirTabelaAsync(TipoTabelaTributaria tabela) => Task.CompletedTask;
        public Task AbrirConfiguracoesAsync() => Task.CompletedTask;
        public Task VoltarAoInicioAsync() => Task.CompletedTask;
    }
    private sealed class PreferenciasDeTeste : IPreferenciasMobile
    {
        private readonly List<string> _favoritos = [];
        public IReadOnlyList<string> Favoritos => _favoritos;
        public void DefinirFavorito(string chave, bool favorito)
        {
            _favoritos.Remove(chave);
            if (favorito) _favoritos.Add(chave);
        }
        public bool AtualizarTabelasAoAbrir { get; set; }
        public DateTime? HorarioAtualizacaoTabelas { get; set; }
        public TemaMobile Tema { get; set; }
    }
    private sealed class PlataformaDeTeste : IPlataformaMobile
    {
        public string Versao => "1.0";
        public Task MostrarAsync(string titulo, string mensagem) => Task.CompletedTask;
        public Task<bool> ConfirmarAsync(string titulo, string mensagem, string aceitar, string cancelar) => Task.FromResult(true);
        public Task AbrirEnderecoAsync(Uri endereco) => Task.CompletedTask;
        public void AplicarTema(TemaMobile tema) { }
    }
    private sealed class AtualizacaoDeTeste : IAtualizarTodasTabelasUseCase
    {
        public int Total => 16;
        public Task<IReadOnlyList<AtualizacaoDeTabela>> ExecutarAsync(IProgress<ProgressoAtualizacaoTabelas>? progresso,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AtualizacaoDeTabela>>([]);
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
