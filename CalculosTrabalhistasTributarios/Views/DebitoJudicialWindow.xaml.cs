using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

public partial class DebitoJudicialWindow : Window
{
    public DebitoJudicialWindow(DebitoJudicialViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
