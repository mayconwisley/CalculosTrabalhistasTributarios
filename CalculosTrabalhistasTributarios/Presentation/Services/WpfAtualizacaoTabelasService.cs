using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using CalculosTrabalhistasTributarios.Views;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

/// <summary>
/// Reaproveita a tela de abertura para mostrar o andamento. A janela principal fica desabilitada enquanto as tabelas são
/// gravadas, para nenhum cálculo ler uma tabela pela metade.
/// </summary>
public sealed class WpfAtualizacaoTabelasService(IAtualizarTodasTabelasUseCase atualizarTabelas, IRegistroDeErros registroDeErros) : IAtualizacaoTabelasService
{
    public async Task<IReadOnlyList<AtualizacaoDeTabela>> AtualizarAgoraAsync()
    {
        // Um modelo novo a cada vez: o cancelamento de uma atualização interrompida não passa para a próxima.
        var viewModel = new AberturaViewModel(atualizarTabelas, registroDeErros);
        var principal = System.Windows.Application.Current.MainWindow;
        var janela = new AberturaWindow(viewModel) { Owner = principal, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        principal.IsEnabled = false;
        try
        {
            janela.Show();
            await viewModel.AtualizarAgoraAsync(DateTime.Now);
            return viewModel.Resultados;
        }
        finally
        {
            principal.IsEnabled = true;
            janela.Close();
            principal.Activate();
        }
    }
}
