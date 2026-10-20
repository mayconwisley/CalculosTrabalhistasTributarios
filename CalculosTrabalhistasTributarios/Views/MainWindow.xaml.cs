using CalculosTrabalhistasTributarios.Presentation.Behaviors;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace CalculosTrabalhistasTributarios.Views;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private Window? _janelaHospedada;
    private readonly List<InputBinding> _atalhosHospedados = [];

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        JanelaLembrada.SetChave(this, "Principal");
        // Depois de exibida: a consulta da versão no GitHub não atrasa a abertura.
        ContentRendered += async (_, _) => await viewModel.CarregarAvisosAsync();
        // Também ao voltar de uma calculadora, que pode ter salvo um cálculo no histórico.
        Activated += async (_, _) => await viewModel.AtualizarRecentesAsync();
    }

    public bool Exibir(Window janela, string chave)
    {
        if (!LiberarConteudo()) return false;

        // A raiz visual da tela é reutilizada; o ViewModel e os comandos continuam sendo os da calculadora.
        if (janela.Content is not FrameworkElement conteudo)
            throw new InvalidOperationException("A tela selecionada não contém um formulário visual.");
        janela.Content = null;
        conteudo.DataContext = janela.DataContext;
        conteudo.Resources.MergedDictionaries.Add(janela.Resources);
        _janelaHospedada = janela;
        if (janela.DataContext is CalculadoraViewModel calculadoraComAtalhos)
        {
            _atalhosHospedados.Add(new KeyBinding(calculadoraComAtalhos.Historico.SalvarCommand, Key.S, ModifierKeys.Control));
            _atalhosHospedados.Add(new KeyBinding(calculadoraComAtalhos.ExportarPdfCommand, Key.P, ModifierKeys.Control));
            _atalhosHospedados.Add(new KeyBinding(calculadoraComAtalhos.ExportarExcelCommand, Key.E, ModifierKeys.Control));
            foreach (var atalho in _atalhosHospedados)
                InputBindings.Add(atalho);
        }
        Hospedeiro.Content = conteudo;
        TituloConteudo.Text = janela.DataContext switch
        {
            CalculadoraViewModel calculadoraAtual => calculadoraAtual.Titulo,
            TabelaManutencaoViewModel tabela => tabela.Titulo,
            _ => janela.Title
        };
        RotuloSecao.Text = chave.StartsWith("Tabela.", StringComparison.Ordinal) ? "Tabelas / " : "Calculadoras / ";
        _viewModel.DefinirAtivo(chave);
        Dashboard.Visibility = Visibility.Collapsed;
        ConteudoInterno.Visibility = Visibility.Visible;
        if (janela.DataContext is CalculadoraViewModel calculadora)
            calculadora.MarcarComoSalvo();
        if (janela.DataContext is TabelaManutencaoViewModel tabelaAberta)
            tabelaAberta.CarregarCommand.Execute(null);
        return true;
    }

    private bool LiberarConteudo()
    {
        if (_janelaHospedada is null) return true;
        if (_janelaHospedada.DataContext is CalculadoraViewModel calculadora && !calculadora.ConfirmarFechamento())
            return false;
        Hospedeiro.Content = null;
        foreach (var atalho in _atalhosHospedados)
            InputBindings.Remove(atalho);
        _atalhosHospedados.Clear();
        _janelaHospedada.Close();
        _janelaHospedada = null;
        return true;
    }

    private void Inicio_Click(object sender, RoutedEventArgs e) => MostrarInicio();
    private void Tabelas_Click(object sender, RoutedEventArgs e)
    {
        if (MostrarInicio())
        {
            _viewModel.Busca = string.Empty;
            AbaTabelas.IsChecked = true;
        }
    }
    private void Historico_Click(object sender, RoutedEventArgs e)
    {
        if (MostrarInicio()) AbaHistorico.IsChecked = true;
    }

    private bool MostrarInicio()
    {
        if (!LiberarConteudo()) return false;
        _viewModel.DefinirAtivo(null);
        ConteudoInterno.Visibility = Visibility.Collapsed;
        Dashboard.Visibility = Visibility.Visible;
        AbaCalculadoras.IsChecked = true;
        return true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!LiberarConteudo()) e.Cancel = true;
        base.OnClosing(e);
    }

    // Ctrl+F leva à busca lateral.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            BuscaLateral.Focus();
            BuscaLateral.SelectAll();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None) return;
        if (BuscaLateral.IsKeyboardFocusWithin && _viewModel.Busca.Length > 0)
        {
            _viewModel.Busca = string.Empty;
            e.Handled = true;
        }
        else if (ConteudoInterno.Visibility == Visibility.Visible)
            e.Handled = MostrarInicio();
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
