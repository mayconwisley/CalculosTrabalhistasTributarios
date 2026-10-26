using System.Windows;

namespace CalculosTrabalhistasTributarios.Presentation.Behaviors;

/// <summary>
/// Campo com erro a corrigir: o modelo da caixa de texto mantém a borda vermelha mesmo com o foco, que o cálculo leva
/// justamente ao primeiro campo a corrigir. Uma borda definida no estilo perderia para a borda de foco do modelo.
/// </summary>
public static class DestaqueDeErro
{
    public static readonly DependencyProperty AtivoProperty = DependencyProperty.RegisterAttached(
        "Ativo", typeof(bool), typeof(DestaqueDeErro), new PropertyMetadata(false));

    public static bool GetAtivo(DependencyObject objeto) => (bool)objeto.GetValue(AtivoProperty);

    public static void SetAtivo(DependencyObject objeto, bool valor) => objeto.SetValue(AtivoProperty, valor);
}
