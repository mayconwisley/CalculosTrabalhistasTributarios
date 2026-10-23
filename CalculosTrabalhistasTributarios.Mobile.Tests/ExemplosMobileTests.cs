using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Local;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using CalculosTrabalhistasTributarios.Mobile.Presentation;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Exemplos;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Tributacao;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

/// <summary>Cada calculadora tem o exemplo do manual, e ele calcula com as tabelas distribuídas, sem mensagem de erro.</summary>
public sealed class ExemplosMobileTests : IClassFixture<ExemplosMobileTests.Ambiente>
{
    private readonly Ambiente _ambiente;

    public ExemplosMobileTests(Ambiente ambiente) => _ambiente = ambiente;

    public static TheoryData<TipoCalculadoraMobile> Tipos => new(Enum.GetValues<TipoCalculadoraMobile>());

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Exemplo_preenche_e_calcula(TipoCalculadoraMobile tipo)
    {
        Assert.True(ExemplosMobile.Existe(tipo), $"Falta Exemplos/Calculadora.{tipo}.json.");
        await _ambiente.PrepararAsync();
        using var scope = _ambiente.Provider.CreateScope();
        var formulario = Criar(scope.ServiceProvider, tipo);
        var vazio = Criar(scope.ServiceProvider, tipo).ExportarDados();

        var exemplo = ExemplosMobile.Montar(tipo, vazio);
        Assert.True(exemplo.Sucesso, exemplo.Sucesso ? null : exemplo.Erro.Mensagem);
        var importacao = formulario.TentarImportarDados(exemplo.Valor);
        Assert.True(importacao.Sucesso, importacao.Sucesso ? null : importacao.Erro.Mensagem);
        var (demonstrativo, mensagem) = await CalcularAsync(formulario);

        Assert.Null(mensagem);
        Assert.NotNull(demonstrativo);
    }

    [Fact]
    public async Task Exemplo_volta_ao_valor_inicial_os_campos_que_nao_traz()
    {
        await _ambiente.PrepararAsync();
        using var scope = _ambiente.Provider.CreateScope();
        var formulario = (CalculadoraTrabalhistaViewModel)Criar(scope.ServiceProvider, TipoCalculadoraMobile.Ferias);
        var vazio = formulario.ExportarDados();
        var previdencia = Assert.Single(formulario.Campos, campo => campo.Chave == "previdencia");
        previdencia.Valor = "999,00";

        var exemplo = ExemplosMobile.Montar(TipoCalculadoraMobile.Ferias, vazio);
        Assert.True(formulario.TentarImportarDados(exemplo.Valor).Sucesso);

        Assert.Equal(vazio.Campos["previdencia"].Valor, previdencia.Valor);
    }

    private static IFormularioHistoricoMobile Criar(IServiceProvider services, TipoCalculadoraMobile tipo) => tipo switch
    {
        TipoCalculadoraMobile.SaqueAniversario => services.GetRequiredService<SaqueAniversarioViewModel>(),
        TipoCalculadoraMobile.SimulacaoTributaria => services.GetRequiredService<SimulacaoTributariaViewModel>(),
        _ => (IFormularioHistoricoMobile)services.GetRequiredService(CatalogoCalculadorasMobile.TipoViewModel(tipo))
    };

    private static async Task<(DemonstrativoDto? Demonstrativo, string? Mensagem)> CalcularAsync(IFormularioHistoricoMobile formulario)
    {
        switch (formulario)
        {
            case CalculadoraTrabalhistaViewModel trabalhista:
                await trabalhista.CalcularCommand.ExecuteAsync(null);
                return (trabalhista.Demonstrativo, trabalhista.Mensagem);
            case SaqueAniversarioViewModel saque:
                await saque.CalcularCommand.ExecuteAsync(null);
                return (saque.Demonstrativo, saque.Mensagem);
            case SimulacaoTributariaViewModel tributaria:
                await tributaria.CalcularCommand.ExecuteAsync(null);
                return (tributaria.Demonstrativo, tributaria.Mensagem);
            default:
                throw new InvalidOperationException(formulario.GetType().Name);
        }
    }

    /// <summary>Cópia isolada do banco distribuído, preparada como no aparelho.</summary>
    public sealed class Ambiente : IDisposable
    {
        private readonly string _pasta = Path.Combine(Path.GetTempPath(), "CalculosMobileTests", Guid.NewGuid().ToString("N"));
        private readonly PreparadorBancoLocal _preparador;
        public ServiceProvider Provider { get; }

        public Ambiente()
        {
            var caminho = Path.Combine(_pasta, "calculoIrrf.db");
            var servicos = new ServiceCollection().AddCalculadoras().AddSaqueAniversario().AddPresentation();
            servicos.AddInfrastructureLocal(caminho);
            servicos.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            servicos.AddSingleton<ICompartilhamentoPdf, PdfDeTeste>();
            servicos.AddSingleton<INavegacaoCalculadoras, NavegacaoDeTeste>();
            Provider = servicos.BuildServiceProvider();
            _preparador = new PreparadorBancoLocal(caminho,
                _ => Task.FromResult<Stream>(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "BancoDados", "base.db"))),
                new InicializadorBancoTributario(Provider.GetRequiredService<BancoTributario>()), Provider.GetRequiredService<SqliteTributacaoConsulta>());
        }

        public Task PrepararAsync() => _preparador.PrepararAsync(CancellationToken.None);

        public void Dispose()
        {
            _preparador.Dispose();
            Provider.Dispose();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
        }
    }

    private sealed class NavegacaoDeTeste : INavegacaoCalculadoras
    {
        public Task AbrirAsync(TipoCalculadoraMobile calculadora) => Task.CompletedTask;
        public Task AbrirAsync(TipoCalculadoraMobile calculadora, CalculosTrabalhistasTributarios.Domain.Trabalhista.QuitacaoBancoHoras quitacao) => Task.CompletedTask;
    }

    private sealed class PdfDeTeste : ICompartilhamentoPdf
    {
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
