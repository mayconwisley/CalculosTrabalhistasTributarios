using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CalculosTrabalhistasTributarios.Presentation.Behaviors;

/// <summary>
/// No ScrollViewer que rola a tela inteira de uma calculadora, a roda do mouse rola primeiro a área interna sob o cursor,
/// como uma lista ou uma grade com barra própria, e passa para a tela quando essa área chega ao início ou ao fim. Sem isso,
/// o ScrollViewer interno fica com a roda mesmo sem ter o que rolar, e a tela para de rolar sobre ele.
/// </summary>
public static class RolagemDaTela
{
    public static readonly DependencyProperty AtivaProperty = DependencyProperty.RegisterAttached(
        "Ativa",
        typeof(bool),
        typeof(RolagemDaTela),
        new PropertyMetadata(false, AoAlterarAtiva));

    public static bool GetAtiva(DependencyObject objeto) => (bool)objeto.GetValue(AtivaProperty);

    public static void SetAtiva(DependencyObject objeto, bool valor) => objeto.SetValue(AtivaProperty, valor);

    private static void AoAlterarAtiva(DependencyObject objeto, DependencyPropertyChangedEventArgs argumentos)
    {
        if (objeto is not ScrollViewer tela)
            return;

        tela.PreviewMouseWheel -= RolarTela;
        if ((bool)argumentos.NewValue)
            tela.PreviewMouseWheel += RolarTela;
    }

    private static void RolarTela(object sender, MouseWheelEventArgs argumentos)
    {
        var tela = (ScrollViewer)sender;

        // Com o mouse capturado, como na lista aberta de um ComboBox, que fica em outra árvore visual, a roda segue o padrão.
        if (Mouse.Captured is not null || argumentos.OriginalSource is not Visual origem || !tela.IsAncestorOf(origem))
            return;

        for (DependencyObject elemento = origem; elemento is not null && elemento != tela; elemento = VisualTreeHelper.GetParent(elemento))
        {
            if (elemento is ScrollViewer interno && PodeRolar(interno, argumentos.Delta))
                return;
        }

        argumentos.Handled = true;
        tela.RaiseEvent(new MouseWheelEventArgs(argumentos.MouseDevice, argumentos.Timestamp, argumentos.Delta) { RoutedEvent = UIElement.MouseWheelEvent });
    }

    // Meia unidade de folga para os arredondamentos do layout; nas grades que rolam por linha, a posição é sempre inteira.
    private static bool PodeRolar(ScrollViewer area, int delta) =>
        delta > 0 ? area.VerticalOffset > 0.5 : area.VerticalOffset < area.ScrollableHeight - 0.5;
}
