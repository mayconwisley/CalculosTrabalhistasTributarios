using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

public partial class PensaoAtrasoWindow : Window
{
    public PensaoAtrasoWindow(PensaoAtrasoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
