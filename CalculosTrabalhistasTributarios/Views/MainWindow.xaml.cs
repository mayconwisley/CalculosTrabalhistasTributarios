using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace CalculosTrabalhistasTributarios.Views;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        // Depois de exibida: a consulta da versão no GitHub não atrasa a abertura.
        ContentRendered += async (_, _) => await viewModel.CarregarAvisosAsync();
    }

    // Ctrl+F leva à busca de cartões (saindo do Histórico, que tem a sua); Esc na busca a limpa.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (AbaHistorico.IsChecked == true)
                AbaCalculadoras.IsChecked = true;
            CampoBusca.Focus();
            CampoBusca.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && CampoBusca.IsKeyboardFocusWithin && _viewModel.Busca.Length > 0)
        {
            _viewModel.Busca = string.Empty;
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }

    private void LimparBusca_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Busca = string.Empty;
        CampoBusca.Focus();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
