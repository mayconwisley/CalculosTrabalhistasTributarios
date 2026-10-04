using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

public partial class CalculadoraWindow : Window
{
    public CalculadoraWindow(CalculadoraViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
