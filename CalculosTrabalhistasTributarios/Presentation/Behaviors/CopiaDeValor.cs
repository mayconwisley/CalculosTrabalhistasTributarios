using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace CalculosTrabalhistasTributarios.Presentation.Behaviors;

/// <summary>
/// Botão com um valor do demonstrativo: o clique (ou Enter e Espaço) copia o valor para a área de transferência, pronto para
/// colar na folha ou numa planilha, e confirma com uma dica junto do valor.
/// </summary>
public static class CopiaDeValor
{
    public static readonly DependencyProperty AtivaProperty = DependencyProperty.RegisterAttached(
        "Ativa", typeof(bool), typeof(CopiaDeValor), new PropertyMetadata(false, AoAlterarAtiva));

    public static bool GetAtiva(DependencyObject objeto) => (bool)objeto.GetValue(AtivaProperty);

    public static void SetAtiva(DependencyObject objeto, bool valor) => objeto.SetValue(AtivaProperty, valor);

    /// <summary>
    /// O texto colado: sem "R$" e sem o sinal de mais, que a planilha não leria como número; "-R$ 10,00" vira "-10,00".
    /// Percentuais, datas e outros textos ficam como estão.
    /// </summary>
    public static string Texto(string valor)
    {
        var texto = valor.Replace(' ', ' ').Trim();
        var negativo = texto.StartsWith('-');
        if (texto.StartsWith('+') || negativo)
            texto = texto[1..].TrimStart();
        if (!texto.StartsWith("R$", StringComparison.Ordinal))
            return valor.Trim();
        texto = texto[2..].Trim();
        return negativo ? "-" + texto : texto;
    }

    private static void AoAlterarAtiva(DependencyObject objeto, DependencyPropertyChangedEventArgs argumentos)
    {
        if (objeto is not ButtonBase botao)
            return;
        botao.Click -= Copiar;
        if ((bool)argumentos.NewValue)
            botao.Click += Copiar;
    }

    private static void Copiar(object sender, RoutedEventArgs argumentos)
    {
        var botao = (ButtonBase)sender;
        if (botao.Content is not string valor || valor.Trim().Length == 0)
            return;
        var texto = Texto(valor);
        string confirmacao;
        try
        {
            Clipboard.SetText(texto);
            confirmacao = $"Copiado: {texto}";
        }
        catch (COMException)
        {
            // Outro programa pode estar com a área de transferência aberta; uma nova tentativa costuma funcionar.
            confirmacao = "Não foi possível copiar agora; tente de novo.";
        }
        Confirmar(botao, confirmacao);
    }

    private static void Confirmar(ButtonBase botao, string mensagem)
    {
        var dica = new ToolTip { Content = mensagem, PlacementTarget = botao, Placement = PlacementMode.Top, IsOpen = true };
        var tempo = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        tempo.Tick += (_, _) =>
        {
            tempo.Stop();
            dica.IsOpen = false;
        };
        tempo.Start();
    }
}
