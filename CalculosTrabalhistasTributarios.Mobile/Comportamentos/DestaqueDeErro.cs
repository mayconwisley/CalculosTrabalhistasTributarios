using CalculosTrabalhistasTributarios.Mobile.Services;
using CalculosTrabalhistasTributarios.Mobile.Views;

namespace CalculosTrabalhistasTributarios.Mobile.Comportamentos;

/// <summary>
/// Erros de preenchimento das páginas próprias, como a do bruto ao líquido, no padrão dos campos comuns: ligado a
/// <c>Erros[Propriedade]</c>, a caixa do campo fica com a borda vermelha e o rótulo abaixo dela mostra o que corrigir.
/// </summary>
public static class DestaqueDeErro
{
    public static readonly BindableProperty MensagemProperty = BindableProperty.CreateAttached("Mensagem", typeof(string), typeof(DestaqueDeErro), null,
        propertyChanged: AoAlterar);

    public static string? GetMensagem(BindableObject elemento) => (string?)elemento.GetValue(MensagemProperty);
    public static void SetMensagem(BindableObject elemento, string? valor) => elemento.SetValue(MensagemProperty, valor);

    private static void AoAlterar(BindableObject elemento, object? antigo, object? novo)
    {
        var mensagem = novo as string;
        var ativo = !string.IsNullOrEmpty(mensagem);
        switch (elemento)
        {
            // Sem erro, a caixa volta à borda do estilo CaixaCampo.
            case Border caixa when ativo:
                caixa.Stroke = (Brush)Microsoft.Maui.Controls.Application.Current!.Resources["ErroCampoBrush"];
                caixa.StrokeThickness = 2;
                SemanticProperties.SetHint(caixa, mensagem);
                break;
            case Border caixa:
                caixa.ClearValue(Border.StrokeProperty);
                caixa.ClearValue(Border.StrokeThicknessProperty);
                caixa.ClearValue(SemanticProperties.HintProperty);
                break;
            case Label rotulo:
                rotulo.Text = mensagem;
                rotulo.IsVisible = ativo;
                break;
        }
    }

    /// <summary>Rola até a primeira caixa destacada, na ordem da página, e leva o foco à entrada dela.</summary>
    public static async Task FocarPrimeiroAsync(Page pagina, ScrollView rolagem)
    {
        var caixa = ApoioFormularioMobile.Descendentes(pagina).OfType<Border>()
            .FirstOrDefault(item => item.IsVisible && !string.IsNullOrEmpty(GetMensagem(item)));
        if (caixa is null || GetMensagem(caixa) is not { } mensagem) return;
        await rolagem.ScrollToAsync(caixa, ScrollToPosition.Start, false);
        switch (caixa.Content)
        {
            case EntradaPeriodoView periodo: periodo.FocarEntrada(); break;
            case VisualElement entrada: entrada.Focus(); break;
        }
        SemanticScreenReader.Default.Announce(mensagem);
    }
}
