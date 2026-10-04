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
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
