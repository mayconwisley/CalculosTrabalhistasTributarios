using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CalculosTrabalhistasTributarios.Presentation.Behaviors;

/// <summary>
/// Campo com erro a corrigir: o modelo da caixa de texto mantém a borda vermelha mesmo com o foco, que o cálculo leva
/// justamente ao primeiro campo a corrigir. Uma borda definida no estilo perderia para a borda de foco do modelo.
/// </summary>
public static class DestaqueDeErro
{
    public static readonly DependencyProperty AtivoProperty = DependencyProperty.RegisterAttached(
        "Ativo", typeof(bool), typeof(DestaqueDeErro), new PropertyMetadata(false));

    /// <summary>
    /// O que corrigir, nas janelas próprias que ligam o campo a <c>Erros[Propriedade]</c>: com texto, o campo fica destacado
    /// e o leitor de tela recebe a orientação como ajuda do controle.
    /// </summary>
    public static readonly DependencyProperty MensagemProperty = DependencyProperty.RegisterAttached(
        "Mensagem", typeof(string), typeof(DestaqueDeErro), new PropertyMetadata(null, AoMudarMensagem));

    public static bool GetAtivo(DependencyObject objeto) => (bool)objeto.GetValue(AtivoProperty);

    public static void SetAtivo(DependencyObject objeto, bool valor) => objeto.SetValue(AtivoProperty, valor);

    public static string? GetMensagem(DependencyObject objeto) => (string?)objeto.GetValue(MensagemProperty);

    public static void SetMensagem(DependencyObject objeto, string? valor) => objeto.SetValue(MensagemProperty, valor);

    /// <summary>
    /// Leva o foco ao primeiro campo destacado sempre que os erros apontarem campos. A raiz é o conteúdo da janela, que a
    /// janela principal hospeda fora da janela original.
    /// </summary>
    public static void Acompanhar(FrameworkElement raiz, ErrosDoFormulario erros) =>
        erros.CampoComErro += () => raiz.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FocarPrimeiro(raiz));

    /// <summary>Leva o foco ao primeiro campo destacado, na ordem da tela; uma grade só é trazida à vista.</summary>
    public static void FocarPrimeiro(DependencyObject raiz)
    {
        if (Primeiro(raiz) is not FrameworkElement elemento)
            return;
        elemento.BringIntoView();
        if (elemento is not Control { IsTabStop: true } controle || !controle.Focus())
            return;
        if (controle is TextBox texto)
            texto.SelectAll();
    }

    private static void AoMudarMensagem(DependencyObject objeto, DependencyPropertyChangedEventArgs argumentos)
    {
        var mensagem = argumentos.NewValue as string;
        SetAtivo(objeto, !string.IsNullOrEmpty(mensagem));
        if (string.IsNullOrEmpty(mensagem))
            objeto.ClearValue(AutomationProperties.HelpTextProperty);
        else
            AutomationProperties.SetHelpText(objeto, mensagem);
    }

    private static DependencyObject? Primeiro(DependencyObject elemento)
    {
        for (var indice = 0; indice < VisualTreeHelper.GetChildrenCount(elemento); indice++)
        {
            var filho = VisualTreeHelper.GetChild(elemento, indice);
            if (filho is UIElement { IsVisible: true } visivel && GetAtivo(visivel))
                return visivel;
            if (Primeiro(filho) is { } encontrado)
                return encontrado;
        }
        return null;
    }
}
