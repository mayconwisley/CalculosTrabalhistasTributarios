using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using CalculosTrabalhistasTributarios.Views;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

public sealed class WpfComparacaoHistoricoService : IComparacaoHistoricoService
{
    public void Mostrar(CalculoSalvoDto primeiro, CalculoSalvoDto segundo)
    {
        var janela = new ComparacaoHistoricoWindow(new ComparacaoHistoricoViewModel(primeiro, segundo));
        var aplicativo = System.Windows.Application.Current;
        janela.Owner = aplicativo.Windows.OfType<Window>().FirstOrDefault(aberta => aberta.IsActive) ?? aplicativo.MainWindow;
        janela.ShowDialog();
    }
}
