namespace CalculosTrabalhistasTributarios.Mobile.Comportamentos;

/// <summary>
/// Vibração curta ao tocar em botões que mudam o formulário, como Calcular, remover ou desfazer. Segue a configuração de
/// toque do Android: com a vibração do toque desligada no aparelho, nada acontece.
/// </summary>
public static class RetornoTatil
{
    public static readonly BindableProperty AtivoProperty = BindableProperty.CreateAttached("Ativo", typeof(bool), typeof(RetornoTatil), false,
        propertyChanged: AoAlterar);

    public static bool GetAtivo(BindableObject elemento) => (bool)elemento.GetValue(AtivoProperty);
    public static void SetAtivo(BindableObject elemento, bool valor) => elemento.SetValue(AtivoProperty, valor);

    private static void AoAlterar(BindableObject elemento, object antigo, object novo)
    {
        if (elemento is not Button botao) return;
        botao.Clicked -= Vibrar;
        if ((bool)novo) botao.Clicked += Vibrar;
    }

    private static void Vibrar(object? sender, EventArgs e)
    {
        // Aparelhos sem motor de vibração não suportam o recurso; o toque continua funcionando.
        if (HapticFeedback.Default.IsSupported) HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }
}
