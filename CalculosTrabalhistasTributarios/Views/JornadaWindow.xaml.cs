using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

public partial class JornadaWindow : Window
{
    public JornadaWindow(JornadaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
