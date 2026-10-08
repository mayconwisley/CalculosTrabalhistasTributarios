using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

public partial class ComparacaoHistoricoWindow : Window
{
    public ComparacaoHistoricoWindow(ComparacaoHistoricoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
