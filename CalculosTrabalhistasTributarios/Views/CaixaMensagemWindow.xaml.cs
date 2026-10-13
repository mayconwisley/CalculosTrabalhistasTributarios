using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Views;

/// <summary>
/// Caixa de mensagem do aplicativo, no lugar do MessageBox do Windows: segue o tema e não toca o som do sistema. Na
/// pergunta, Não é o botão padrão, como antes, e Esc também responde Não. Ctrl+C copia o título e a mensagem.
/// </summary>
public partial class CaixaMensagemWindow : Window
{
    private readonly string _mensagem;

    private CaixaMensagemWindow(string mensagem, string titulo, TipoCaixaMensagem tipo)
    {
        InitializeComponent();
        _mensagem = mensagem;
        Title = titulo;
        Texto.Text = mensagem;
        AutomationProperties.SetName(Texto, mensagem);

        var (simbolo, fundo, borda, cor) = tipo switch
        {
            TipoCaixaMensagem.Aviso => ("!", "WarningBackgroundBrush", "WarningBorderBrush", "WarningTextBrush"),
            TipoCaixaMensagem.Erro => ("×", "ErrorBackgroundBrush", "ErrorBorderBrush", "ErrorTextBrush"),
            TipoCaixaMensagem.Pergunta => ("?", "ResultBackgroundBrush", "AccentBrush", "AccentBrush"),
            _ => ("i", "ResultBackgroundBrush", "AccentBrush", "AccentBrush")
        };
        Simbolo.Text = simbolo;
        Icone.SetResourceReference(BackgroundProperty, fundo);
        Icone.SetResourceReference(BorderBrushProperty, borda);
        Simbolo.SetResourceReference(ForegroundProperty, cor);

        if (tipo == TipoCaixaMensagem.Pergunta)
        {
            Sim.Visibility = Visibility.Visible;
            Ok.Content = "Não";
            AutomationProperties.SetName(Ok, "Não");
            // O destaque fica no Sim, a ação proposta; Enter e Esc continuam respondendo Não, a resposta segura.
            Ok.ClearValue(StyleProperty);
            Sim.SetResourceReference(StyleProperty, "PrimaryButton");
        }
        Loaded += (_, _) => Ok.Focus();
    }

    /// <summary>Mostra a mensagem sobre a janela ativa; na pergunta, devolve verdadeiro só quando a resposta é Sim.</summary>
    public static bool Mostrar(string mensagem, string titulo, TipoCaixaMensagem tipo, Window? dono = null)
    {
        var aplicativo = System.Windows.Application.Current;
        dono ??= aplicativo?.Windows.OfType<Window>().FirstOrDefault(janela => janela.IsActive)
                 ?? (aplicativo?.MainWindow is { IsVisible: true } principal ? principal : null);
        var caixa = new CaixaMensagemWindow(mensagem, titulo, tipo);
        if (dono is not null && !ReferenceEquals(dono, caixa))
            caixa.Owner = dono;
        else
        {
            caixa.ShowInTaskbar = true;
            caixa.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        return caixa.ShowDialog() == true;
    }

    private void AoConfirmar(object sender, RoutedEventArgs e) => DialogResult = true;

    private void AoFechar(object sender, RoutedEventArgs e) => DialogResult = false;

    private void AoCopiar(object sender, ExecutedRoutedEventArgs e) => Clipboard.SetText($"{Title}{Environment.NewLine}{Environment.NewLine}{_mensagem}");
}
