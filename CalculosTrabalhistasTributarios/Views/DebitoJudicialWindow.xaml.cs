using CalculosTrabalhistasTributarios.Presentation.Behaviors;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

public partial class DebitoJudicialWindow : Window
{
    public DebitoJudicialWindow(DebitoJudicialViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        JanelaLembrada.SetChave(this, viewModel.TipoHistorico);
        DestaqueDeErro.Acompanhar((FrameworkElement)Content, viewModel.Erros);
    }
}
