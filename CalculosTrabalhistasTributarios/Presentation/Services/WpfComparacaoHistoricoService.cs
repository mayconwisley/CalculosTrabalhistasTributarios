using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using CalculosTrabalhistasTributarios.Views;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

public sealed class WpfComparacaoHistoricoService(IRecalculoHistorico recalculo) : IComparacaoHistoricoService
{
    public async Task MostrarAsync(CalculoSalvoDto primeiro, CalculoSalvoDto segundo)
    {
        var viewModel = recalculo.Suporta(primeiro) && recalculo.Suporta(segundo)
            ? new ComparacaoHistoricoViewModel(primeiro, segundo,
                await recalculo.RecalcularAsync(primeiro, CancellationToken.None), await recalculo.RecalcularAsync(segundo, CancellationToken.None))
            : new ComparacaoHistoricoViewModel(primeiro, segundo);
        Mostrar(viewModel);
    }

    public void MostrarCenarios(CalculoSalvoDto cenarioA, DemonstrativoDto resultadoA, CalculoSalvoDto cenarioB, DemonstrativoDto resultadoB) =>
        Mostrar(new ComparacaoHistoricoViewModel(cenarioA, cenarioB, resultadoA, resultadoB, cenarios: true));

    private static void Mostrar(ComparacaoHistoricoViewModel viewModel)
    {
        var janela = new ComparacaoHistoricoWindow(viewModel);
        var aplicativo = System.Windows.Application.Current;
        janela.Owner = aplicativo.Windows.OfType<Window>().FirstOrDefault(aberta => aberta.IsActive) ?? aplicativo.MainWindow;
        janela.ShowDialog();
    }
}
