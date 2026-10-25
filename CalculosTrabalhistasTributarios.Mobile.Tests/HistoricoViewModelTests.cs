using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class HistoricoViewModelTests
{
    private static HistoricoMobileViewModel Criar(IConsultaHistoricoCalculos consulta, IInteracaoHistoricoMobile? interacao = null) => new(consulta, interacao ?? new InteracaoTeste(), NullLogger<HistoricoMobileViewModel>.Instance);
    private static ResumoCalculoSalvoDto Item(long id, string tipo = "Ferias") => new(id, tipo, tipo, $"Cálculo {id}", DateTime.Today, DateTime.Today);
    [Fact]
    public async Task Pagina_desabilita_limites_e_envia_deslocamentos_sem_buscar_formularios()
    {
        var pedidos = new List<ConsultaHistoricoDto>();
        var vm = Criar(new ConsultaTeste((p, _) =>
        {
            pedidos.Add(p);
            var itens = Enumerable.Range(p.Deslocamento, Math.Min(p.Limite, 23 - p.Deslocamento)).Select(i => Item(i + 1)).ToArray();
            return Task.FromResult(new PaginaHistoricoDto(itens, 23, 23, ["Ferias"]));
        }));
        await vm.AtualizarCommand.ExecuteAsync(null);
        Assert.Equal(20, vm.Itens.Count); Assert.False(vm.AnteriorCommand.CanExecute(null)); Assert.True(vm.ProximaCommand.CanExecute(null));
        await vm.ProximaCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.Itens.Count); Assert.Equal(20, pedidos[^1].Deslocamento);
        Assert.False(vm.ProximaCommand.CanExecute(null)); Assert.True(vm.AnteriorCommand.CanExecute(null));
        await vm.AnteriorCommand.ExecuteAsync(null); Assert.Equal(0, pedidos[^1].Deslocamento);
    }
    [Fact]
    public async Task Falha_exibe_mensagem_e_permite_tentar_novamente()
    {
        var falhar = true;
        var vm = Criar(new ConsultaTeste((_, _) => falhar ? throw new IOException() : Task.FromResult(new PaginaHistoricoDto([], 0, 0, []))));
        await vm.AtualizarCommand.ExecuteAsync(null);
        Assert.True(vm.TemMensagem); Assert.Empty(vm.Itens); Assert.False(vm.Ocupado); Assert.True(vm.AtualizarCommand.CanExecute(null));
        Assert.Contains("Atualizar", vm.Vazio); Assert.Empty(vm.ResumoPagina);
        falhar = false; await vm.AtualizarCommand.ExecuteAsync(null);
        Assert.False(vm.TemMensagem); Assert.Contains("Nenhum cálculo salvo", vm.Vazio);
    }
    [Fact]
    public async Task Consulta_antiga_nao_sobrescreve_nova_busca_mesmo_se_fonte_ignorar_cancelamento()
    {
        var antiga = new TaskCompletionSource<PaginaHistoricoDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var novaEntrou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var quantidade = 0;
        var vm = Criar(new ConsultaTeste((p, _) =>
        {
            if (++quantidade == 1) return antiga.Task;
            Assert.Equal("nova", p.Busca); novaEntrou.SetResult();
            return Task.FromResult(new PaginaHistoricoDto([Item(2)], 1, 2, ["Ferias"]));
        }));
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Carregando) && !vm.Carregando) terminou.TrySetResult(); };
        var primeira = vm.AtualizarCommand.ExecuteAsync(null);
        vm.Busca = "nova";
        await novaEntrou.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await terminou.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        antiga.SetResult(new([Item(1)], 1, 2, ["Ferias"]));
        await primeira;
        Assert.Equal(2, Assert.Single(vm.Itens).Id); Assert.False(vm.TemMensagem);
    }
    [Fact]
    public async Task Comparacao_preserva_selecao_entre_paginas_e_bloqueia_tipos_diferentes()
    {
        var interacao = new InteracaoTeste();
        var vm = Criar(new ConsultaTeste((p, _) => Task.FromResult(new PaginaHistoricoDto([Item(p.Deslocamento == 0 ? 1 : 2)], 21, 21, ["Ferias"]))), interacao);
        await vm.AtualizarCommand.ExecuteAsync(null);
        var a = Assert.Single(vm.Itens); vm.SelecionarCommand.Execute(a);
        await vm.ProximaCommand.ExecuteAsync(null);
        Assert.Equal(a, vm.Primeiro);
        var b = Assert.Single(vm.Itens);
        vm.SelecionarCommand.Execute(Item(3, "Rescisao")); Assert.Null(vm.Segundo); Assert.True(vm.TemMensagem);
        vm.SelecionarCommand.Execute(b); Assert.True(vm.CompararCommand.CanExecute(null));
        await vm.CompararCommand.ExecuteAsync(null); Assert.Equal((1L, 2L), interacao.Comparados);
        vm.SelecionarCommand.Execute(a); Assert.Equal(b, vm.Primeiro); Assert.False(vm.CompararCommand.CanExecute(null));
        await vm.ExcluirCommand.ExecuteAsync(b); Assert.Null(vm.Primeiro); Assert.Null(vm.Segundo);
    }
    [Fact]
    public async Task Acao_em_andamento_desabilita_outros_comandos_e_falha_nao_fica_silenciosa()
    {
        var liberacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interacao = new InteracaoTeste { AoAbrir = () => liberacao.Task };
        var vm = Criar(new ConsultaTeste((_, _) => Task.FromResult(new PaginaHistoricoDto([], 0, 0, []))), interacao);
        var abrindo = vm.AbrirCommand.ExecuteAsync(Item(1));
        Assert.True(vm.Ocupado); Assert.False(vm.ExcluirCommand.CanExecute(Item(1))); Assert.False(vm.AtualizarCommand.CanExecute(null));
        liberacao.SetException(new IOException()); await abrindo;
        Assert.False(vm.Ocupado); Assert.True(vm.TemMensagem);
    }
    private sealed class ConsultaTeste(Func<ConsultaHistoricoDto, CancellationToken, Task<PaginaHistoricoDto>> consultar) : IConsultaHistoricoCalculos
    {
        public Task<PaginaHistoricoDto> ConsultarAsync(ConsultaHistoricoDto consulta, CancellationToken cancellationToken) => consultar(consulta, cancellationToken);
    }
    internal sealed class InteracaoTeste : IInteracaoHistoricoMobile
    {
        public Func<Task>? AoAbrir { get; init; }
        public (long, long)? Comparados { get; private set; }
        public Task AbrirAsync(ResumoCalculoSalvoDto salvo) => AoAbrir?.Invoke() ?? Task.CompletedTask;
        public Task CompararAsync(ResumoCalculoSalvoDto a, ResumoCalculoSalvoDto b) { Comparados = (a.Id, b.Id); return Task.CompletedTask; }
        public Task CompartilharPdfAsync(ResumoCalculoSalvoDto salvo) => Task.CompletedTask;
        public Task DuplicarAsync(ResumoCalculoSalvoDto salvo) => Task.CompletedTask;
        public Task RenomearAsync(ResumoCalculoSalvoDto salvo) => Task.CompletedTask;
        public Task ExcluirAsync(ResumoCalculoSalvoDto salvo) => Task.CompletedTask;
    }
}
