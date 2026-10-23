using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class CompartilhamentoPdfTests
{
    [Fact]
    public async Task Compartilha_exatamente_o_demonstrativo_exibido()
    {
        var compartilhamento = new CompartilhamentoDeTeste();
        var vm = Criar(compartilhamento);
        Assert.False(vm.CompartilharPdfCommand.CanExecute(null));
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.True(vm.CompartilharPdfCommand.CanExecute(null));
        await vm.CompartilharPdfCommand.ExecuteAsync(null);
        Assert.Same(vm.Demonstrativo, compartilhamento.Demonstrativo);
        Assert.Equal(2250m, compartilhamento.Demonstrativo?.Resultado);
        Assert.Null(vm.Mensagem); // Abrir o menu não comprova envio ao destinatário.
    }

    [Fact]
    public async Task Editar_formulario_invalida_o_pdf_disponivel()
    {
        var vm = Criar(new CompartilhamentoDeTeste());
        await vm.CalcularCommand.ExecuteAsync(null);
        vm.Saldo = "9000";
        Assert.False(vm.CompartilharPdfCommand.CanExecute(null));
    }

    [Fact]
    public async Task Sem_calculo_nao_invoca_o_servico()
    {
        var compartilhamento = new CompartilhamentoDeTeste();
        var vm = Criar(compartilhamento);
        await vm.CompartilharPdfCommand.ExecuteAsync(null);
        Assert.Null(compartilhamento.Demonstrativo);
    }

    [Fact]
    public async Task Enquanto_compartilha_bloqueia_nova_operacao_e_preserva_resultado()
    {
        var concluir = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = Criar(new CompartilhamentoDeTeste(_ => concluir.Task));
        await vm.CalcularCommand.ExecuteAsync(null);
        var demonstrativo = vm.Demonstrativo;
        var tarefa = vm.CompartilharPdfCommand.ExecuteAsync(null);
        Assert.True(vm.Ocupado);
        Assert.False(vm.CalcularCommand.CanExecute(null));
        Assert.False(vm.CompartilharPdfCommand.CanExecute(null));
        concluir.SetResult();
        await tarefa;
        Assert.False(vm.Ocupado);
        Assert.Same(demonstrativo, vm.Demonstrativo);
        Assert.True(vm.CalcularCommand.CanExecute(null));
    }

    [Fact]
    public async Task Falha_no_compartilhamento_preserva_calculo_e_permite_tentar_novamente()
    {
        var vm = Criar(new CompartilhamentoDeTeste(_ => throw new IOException("Falha de teste.")));
        await vm.CalcularCommand.ExecuteAsync(null);
        await vm.CompartilharPdfCommand.ExecuteAsync(null);
        Assert.Contains("Não foi possível compartilhar", vm.Mensagem);
        Assert.NotNull(vm.Demonstrativo);
        Assert.False(vm.Ocupado);
        Assert.True(vm.CompartilharPdfCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancelamento_nao_e_tratado_como_falha()
    {
        var vm = Criar(new CompartilhamentoDeTeste(token => Task.Delay(Timeout.Infinite, token)));
        await vm.CalcularCommand.ExecuteAsync(null);
        var tarefa = vm.CompartilharPdfCommand.ExecuteAsync(null);
        vm.CompartilharPdfCommand.Cancel();
        await tarefa;
        Assert.Null(vm.Mensagem);
        Assert.False(vm.Ocupado);
        Assert.NotNull(vm.Demonstrativo);
    }

    private static SaqueAniversarioViewModel Criar(ICompartilhamentoPdf compartilhamento) =>
        new(new SimularSaqueAniversarioUseCase(), NullLogger<SaqueAniversarioViewModel>.Instance, compartilhamento)
        { Saldo = "8.000,00", IndiceMes = 3 };

    private sealed class CompartilhamentoDeTeste(Func<CancellationToken, Task>? executar = null) : ICompartilhamentoPdf
    {
        public DemonstrativoDto? Demonstrativo { get; private set; }
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken)
        {
            Demonstrativo = demonstrativo;
            return executar?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }
    }
}
