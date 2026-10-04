using System.Windows;

namespace CalculosTrabalhistasTributarios.Views;

/// <summary>Pede o nome com que o cálculo fica no histórico.</summary>
public partial class NomeCalculoWindow : Window
{
    public NomeCalculoWindow(string titulo, string acao, string nomeAtual, bool oferecerComoNovo)
    {
        InitializeComponent();
        SalvarComoNovo.Visibility = oferecerComoNovo ? Visibility.Visible : Visibility.Collapsed;
        Title = titulo;
        Confirmar.Content = acao;
        Nome.Text = nomeAtual;
        Loaded += (_, _) =>
        {
            Nome.Focus();
            Nome.SelectAll();
        };
    }

    public string NomeInformado => Nome.Text.Trim();
    public bool ComoNovo => SalvarComoNovo.IsChecked == true;

    private void AoConfirmar(object sender, RoutedEventArgs e)
    {
        if (NomeInformado.Length == 0)
        {
            Aviso.Visibility = Visibility.Visible;
            Nome.Focus();
            return;
        }
        DialogResult = true;
    }
}
