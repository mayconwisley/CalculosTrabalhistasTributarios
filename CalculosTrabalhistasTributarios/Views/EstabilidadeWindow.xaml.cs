using CalculosTrabalhistasTributarios.Presentation.Behaviors;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

public partial class EstabilidadeWindow : Window
{
    public EstabilidadeWindow(EstabilidadeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        JanelaLembrada.SetChave(this, viewModel.TipoHistorico);
        DestaqueDeErro.Acompanhar((FrameworkElement)Content, viewModel.Erros);
    }
}
