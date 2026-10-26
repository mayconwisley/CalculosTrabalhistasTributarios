using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CalculosTrabalhistasTributarios.Presentation.Documentos;

namespace CalculosTrabalhistasTributarios.Presentation.Behaviors;

/// <summary>Preenche o TextBlock com os trechos de um documento legal, com o negrito do Markdown em seminegrito.</summary>
public static class TextoComTrechos
{
    public static readonly DependencyProperty TrechosProperty = DependencyProperty.RegisterAttached(
        "Trechos", typeof(IReadOnlyList<TrechoDocumentoLegal>), typeof(TextoComTrechos), new PropertyMetadata(null, AoAlterarTrechos));

    public static IReadOnlyList<TrechoDocumentoLegal>? GetTrechos(DependencyObject objeto) => (IReadOnlyList<TrechoDocumentoLegal>?)objeto.GetValue(TrechosProperty);

    public static void SetTrechos(DependencyObject objeto, IReadOnlyList<TrechoDocumentoLegal>? valor) => objeto.SetValue(TrechosProperty, valor);

    private static void AoAlterarTrechos(DependencyObject objeto, DependencyPropertyChangedEventArgs argumentos)
    {
        if (objeto is not TextBlock texto) return;
        texto.Inlines.Clear();
        foreach (var trecho in (IReadOnlyList<TrechoDocumentoLegal>?)argumentos.NewValue ?? [])
            texto.Inlines.Add(new Run(trecho.Texto) { FontWeight = trecho.Negrito ? FontWeights.SemiBold : texto.FontWeight });
    }
}
