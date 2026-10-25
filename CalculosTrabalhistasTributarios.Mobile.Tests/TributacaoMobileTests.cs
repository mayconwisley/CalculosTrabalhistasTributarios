using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Tributacao;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class TributacaoMobileTests
{
    [Theory]
    [InlineData(2019, 1, 0)]
    [InlineData(2020, 3, 1)]
    [InlineData(2023, 5, 2)]
    [InlineData(2026, 1, 0)]
    [InlineData(2026, 1, 3)]
    public async Task Formulario_mobile_reproduz_o_caso_de_uso_do_wpf(int ano, int mes, int vinculo)
    {
        using var ambiente = new AmbienteLocal();
        var consulta = new TributacaoLocalPreparada(ambiente.Preparador, ambiente.Consulta);
        var casoDeUso = new SimularImpostoUseCase(consulta);
        var vm = new SimulacaoTributariaViewModel(casoDeUso, new CompartilhamentoDeTeste(), NullLogger<SimulacaoTributariaViewModel>.Instance)
        {
            Competencia = $"{mes:00}/{ano}", ValorBruto = "8.000,00", Dependentes = "2", IndiceVinculo = vinculo
        };
        await vm.CalcularCommand.ExecuteAsync(null);
        var esperado = await casoDeUso.ExecutarAsync(new(new DateOnly(ano, mes, 1), 8000m,
            vinculo == 3 ? 0m : 8000m, 2, (VinculoSimulacao)vinculo), TestContext.Current.CancellationToken);
        Assert.True(esperado.Sucesso);
        Assert.Null(vm.Mensagem);
        Assert.NotNull(vm.Demonstrativo);
        Assert.Equal(esperado.Valor.Resultado, vm.Demonstrativo.Resultado);
        Assert.Equal(esperado.Valor.Informativos, vm.Demonstrativo.Informativos);
        Assert.Equal(esperado.Valor.Memoria.SelectMany(g => g.Formulas), vm.Demonstrativo.Memoria.SelectMany(g => g.Formulas));
        Assert.Equal(esperado.Valor.Memoria.SelectMany(g => g.Quadro?.Faixas ?? []),
            vm.Demonstrativo.Memoria.SelectMany(g => g.Quadro?.Faixas ?? []));
        Assert.True(vm.CompartilharPdfCommand.CanExecute(null));
        var apurado = vm.Demonstrativo;
        vm.Dependentes = "3";
        Assert.Null(vm.Demonstrativo);
        Assert.False(vm.CompartilharPdfCommand.CanExecute(null));
        Assert.NotNull(apurado.Comparativo);
    }

    [Fact]
    public async Task Base_distinta_e_aposentadoria_sao_mapeadas_sem_confundir_os_campos()
    {
        using var ambiente = new AmbienteLocal();
        var caso = new SimularImpostoUseCase(new TributacaoLocalPreparada(ambiente.Preparador, ambiente.Consulta));
        var vm = new SimulacaoTributariaViewModel(caso, new CompartilhamentoDeTeste(), NullLogger<SimulacaoTributariaViewModel>.Instance)
        { Competencia = "01/2026", ValorBruto = "8000", UsarBrutoComoBase = false, BaseInss = "3500" };
        await vm.CalcularCommand.ExecuteAsync(null);
        var esperado = await caso.ExecutarAsync(new(new(2026, 1, 1), 8000m, 3500m, 0), TestContext.Current.CancellationToken);
        Assert.Equal(esperado.Valor.Resultado, vm.Demonstrativo?.Resultado);
        vm.IndiceVinculo = 3;
        vm.SessentaECincoAnos = true;
        vm.DecimoTerceiro = true;
        await vm.CalcularCommand.ExecuteAsync(null);
        var aposentadoria = await caso.ExecutarAsync(new(new(2026, 1, 1), 8000m, 0m, 0, VinculoSimulacao.Aposentadoria, true, true), TestContext.Current.CancellationToken);
        Assert.Null(vm.Mensagem);
        Assert.Equal(aposentadoria.Valor.Resultado, vm.Demonstrativo?.Resultado);
    }

    [Theory]
    [InlineData("2026-01", "8000", "0", "Competência")]
    [InlineData("13/2026", "8000", "0", "Competência")]
    [InlineData("01/2026", "", "0", "Valor bruto")]
    [InlineData("01/2026", "abc", "0", "Valor bruto")]
    [InlineData("01/2026", "8000", "1,5", "Dependentes")]
    [InlineData("01/2010", "8000", "0", "tabela")]
    public async Task Dados_invalidos_nao_geram_resultado_nem_pdf(string competencia, string bruto, string dependentes, string campo)
    {
        using var ambiente = new AmbienteLocal();
        var vm = new SimulacaoTributariaViewModel(new SimularImpostoUseCase(new TributacaoLocalPreparada(ambiente.Preparador, ambiente.Consulta)),
            new CompartilhamentoDeTeste(), NullLogger<SimulacaoTributariaViewModel>.Instance)
        { Competencia = competencia, ValorBruto = bruto, Dependentes = dependentes };
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.NotNull(vm.Mensagem);
        Assert.Contains(campo, vm.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Null(vm.Demonstrativo);
        Assert.False(vm.CompartilharPdfCommand.CanExecute(null));
    }

    [Fact]
    public async Task Inicializacao_concorrente_copia_uma_vez_e_preserva_manutencoes_ao_reabrir()
    {
        using var ambiente = new AmbienteLocal();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ambiente.Preparador.PrepararAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(1, ambiente.Copias);
        await using (var conexao = await ambiente.Banco.AbrirAsync(TestContext.Current.CancellationToken))
        {
            using var comando = conexao.CreateCommand();
            comando.CommandText = "CREATE TABLE MarcadorLocal (Valor TEXT); INSERT INTO MarcadorLocal VALUES ('preservado');";
            await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        using var segundaAbertura = new PreparadorBancoLocal(ambiente.Caminho,
            _ => throw new InvalidOperationException("Banco existente não deve ser sobrescrito."),
            new InicializadorBancoTributario(ambiente.Banco), ambiente.Consulta);
        await segundaAbertura.PrepararAsync(TestContext.Current.CancellationToken);
        await using var leitura = await ambiente.Banco.AbrirAsync(TestContext.Current.CancellationToken);
        using var consultar = leitura.CreateCommand();
        consultar.CommandText = "SELECT Valor FROM MarcadorLocal";
        Assert.Equal("preservado", await consultar.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Copia_cancelada_nao_deixa_banco_parcial_e_permite_nova_tentativa()
    {
        using var ambiente = new AmbienteLocal();
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ambiente.Preparador.PrepararAsync(cancelamento.Token));
        Assert.False(File.Exists(ambiente.Caminho));
        await ambiente.Preparador.PrepararAsync(TestContext.Current.CancellationToken);
        Assert.True(File.Exists(ambiente.Caminho));
        Assert.False(File.Exists(ambiente.Caminho + ".tmp"));
    }

    [Fact]
    public void Persistencia_compartilhada_nao_carrega_relatorios_nem_interfaces_graficas()
    {
        var referencias = typeof(BancoTributario).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(referencias, r => r.Name is "QuestPDF" or "ClosedXML" or "CalculosTrabalhistasTributarios.Infrastructure"
            or "CalculosTrabalhistasTributarios" || (r.Name?.StartsWith("Microsoft.Maui", StringComparison.Ordinal) ?? false));
    }

    private sealed class CompartilhamentoDeTeste : ICompartilhamentoPdf
    {
        public Task CompartilharResumoAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AmbienteLocal : IDisposable
    {
        private readonly string _pasta = Path.Combine(Path.GetTempPath(), "CalculosMobileTests", Guid.NewGuid().ToString("N"));
        public string Caminho { get; }
        public BancoTributario Banco { get; }
        public SqliteTributacaoConsulta Consulta { get; }
        public PreparadorBancoLocal Preparador { get; }
        public int Copias { get; private set; }
        public AmbienteLocal()
        {
            Caminho = Path.Combine(_pasta, "calculoIrrf.db");
            Banco = new BancoTributario(new SqliteConnectionStringBuilder { DataSource = Caminho, Pooling = false }.ToString());
            Consulta = new SqliteTributacaoConsulta(Banco);
            Preparador = new PreparadorBancoLocal(Caminho, _ =>
            {
                Copias++;
                return Task.FromResult<Stream>(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "BancoDados", "base.db")));
            }, new InicializadorBancoTributario(Banco), Consulta);
        }
        public void Dispose()
        {
            Preparador.Dispose();
            if (Directory.Exists(_pasta))
                Directory.Delete(_pasta, recursive: true);
        }
    }
}
