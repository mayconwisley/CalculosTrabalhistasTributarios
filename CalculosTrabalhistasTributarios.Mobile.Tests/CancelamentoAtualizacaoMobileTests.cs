using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Abertura;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class CancelamentoAtualizacaoMobileTests
{
    [Fact]
    public async Task Pular_devolve_controle_antes_de_terminar_callback_de_cancelamento()
    {
        using var fonte = new AtualizadorBloqueante();
        var vm = new AtualizacaoTabelasViewModel(fonte, new PreferenciasTeste(), new(), new PlataformaTeste(), NullLogger<AtualizacaoTabelasViewModel>.Instance);
        var abertura = vm.AbrirAsync(_ => Task.CompletedTask, new DateTime(2026, 10, 24));
        await fonte.Iniciada.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var retornou = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var chamada = Task.Run(() => retornou.SetResult(vm.InterromperCommand.ExecuteAsync(null)), TestContext.Current.CancellationToken);
        Task? cancelamento = null;
        try
        {
            await fonte.CallbackIniciado.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            cancelamento = await retornou.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.False(cancelamento.IsCompleted);
        }
        finally { fonte.Liberar.Set(); }
        await chamada;
        if (cancelamento is not null) await cancelamento;
        Assert.True(await abertura);
        Assert.False(vm.Atualizando);
        Assert.Contains("interrompida", vm.Mensagem);
    }

    private sealed class AtualizadorBloqueante : IAtualizarTodasTabelasUseCase, IDisposable
    {
        public int Total => 1;
        public TaskCompletionSource Iniciada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CallbackIniciado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Liberar { get; } = new();
        public async Task<IReadOnlyList<AtualizacaoDeTabela>> ExecutarAsync(IProgress<ProgressoAtualizacaoTabelas>? progresso, CancellationToken cancellationToken)
        {
            var fim = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registro = cancellationToken.Register(() =>
            {
                CallbackIniciado.SetResult();
                Liberar.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
                fim.TrySetCanceled(cancellationToken);
            });
            Iniciada.SetResult();
            await fim.Task;
            return [];
        }
        public void Dispose() => Liberar.Dispose();
    }

    private sealed class PreferenciasTeste : IPreferenciasMobile
    {
        public IReadOnlyList<string> Favoritos => [];
        public void DefinirFavorito(string chave, bool favorito) { }
        public bool AtualizarTabelasAoAbrir { get; set; } = true;
        public DateTime? HorarioAtualizacaoTabelas { get; set; }
        public TemaMobile Tema { get; set; }
        public bool AvisoSimulacaoLido { get; set; }
    }
    private sealed class PlataformaTeste : IPlataformaMobile
    {
        public string Versao => "teste";
        public Task MostrarAsync(string titulo, string mensagem) => Task.CompletedTask;
        public Task<bool> ConfirmarAsync(string titulo, string mensagem, string aceitar, string cancelar) => Task.FromResult(true);
        public Task AbrirEnderecoAsync(Uri endereco) => Task.CompletedTask;
        public void AplicarTema(TemaMobile tema) { }
        public Task CompartilharArquivoAsync(string titulo, string caminho) => Task.CompletedTask;
        public Task CompartilharConteudoAsync(string titulo, string nomeArquivo, string conteudo, string tipoConteudo) => Task.CompletedTask;
        public Task<CalculosTrabalhistasTributarios.Domain.Comum.Result<string>?> EscolherArquivoTextoAsync(string titulo, int limiteCaracteres) =>
            Task.FromResult<CalculosTrabalhistasTributarios.Domain.Comum.Result<string>?>(null);
    }
}
