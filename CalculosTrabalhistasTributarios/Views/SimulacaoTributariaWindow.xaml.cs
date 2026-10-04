using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

public partial class SimulacaoTributariaWindow : Window
{
    public SimulacaoTributariaWindow(SimulacaoTributariaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
