using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

public partial class EstabilidadeWindow : Window
{
    public EstabilidadeWindow(EstabilidadeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
