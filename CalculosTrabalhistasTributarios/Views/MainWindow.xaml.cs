using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Navigation;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;

namespace CalculosTrabalhistasTributarios.Views;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        // Depois de exibida: a consulta da versão no GitHub não atrasa a abertura.
        ContentRendered += async (_, _) => await viewModel.CarregarAvisosAsync();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
