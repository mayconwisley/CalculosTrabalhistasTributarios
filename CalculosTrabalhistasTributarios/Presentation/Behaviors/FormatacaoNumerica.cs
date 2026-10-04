using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CalculosTrabalhistasTributarios.Presentation.Behaviors;

/// <summary>
/// Campos numéricos: um campo zerado é limpo ao receber o foco, para o usuário digitar direto, e um campo deixado vazio
/// volta a zero ao perder o foco. Com <c>Formato</c>, o valor também é formatado ao sair do campo (por exemplo "N2");
/// sem formato, <c>ValorVazio</c> define o zero exibido, como "0" nos inteiros ou "0:00" nas horas.
/// </summary>
public static class FormatacaoNumerica
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static readonly DependencyProperty FormatoProperty = DependencyProperty.RegisterAttached(
        "Formato",
        typeof(string),
        typeof(FormatacaoNumerica),
        new PropertyMetadata(null, AoAlterarConfiguracao));

    public static readonly DependencyProperty ValorVazioProperty = DependencyProperty.RegisterAttached(
        "ValorVazio",
        typeof(string),
        typeof(FormatacaoNumerica),
        new PropertyMetadata(null, AoAlterarConfiguracao));

    public static string GetFormato(DependencyObject objeto) => (string)objeto.GetValue(FormatoProperty);

    public static void SetFormato(DependencyObject objeto, string valor) => objeto.SetValue(FormatoProperty, valor);

    public static string GetValorVazio(DependencyObject objeto) => (string)objeto.GetValue(ValorVazioProperty);

    public static void SetValorVazio(DependencyObject objeto, string valor) => objeto.SetValue(ValorVazioProperty, valor);

    private static void AoAlterarConfiguracao(DependencyObject objeto, DependencyPropertyChangedEventArgs argumentos)
    {
        if (objeto is not TextBox campo)
            return;

        campo.GotKeyboardFocus -= LimparZeroAoReceberFoco;
        campo.LostKeyboardFocus -= FormatarAoPerderFoco;

        if (!string.IsNullOrEmpty(GetFormato(campo)) || !string.IsNullOrEmpty(GetValorVazio(campo)))
        {
            campo.GotKeyboardFocus += LimparZeroAoReceberFoco;
            campo.LostKeyboardFocus += FormatarAoPerderFoco;
        }
    }

    private static void LimparZeroAoReceberFoco(object sender, RoutedEventArgs argumentos)
    {
        if (sender is not TextBox { Text: var texto } campo || !EhZero(texto))
            return;

        campo.Clear();
        BindingOperations.GetBindingExpression(campo, TextBox.TextProperty)?.UpdateSource();
    }

    private static void FormatarAoPerderFoco(object sender, RoutedEventArgs argumentos)
    {
        if (sender is not TextBox { Text: var texto } campo)
            return;

        var formato = GetFormato(campo);
        if (string.IsNullOrWhiteSpace(texto))
        {
            campo.Text = string.IsNullOrEmpty(formato) ? GetValorVazio(campo) : 0m.ToString(formato, CulturaPtBr);
            BindingOperations.GetBindingExpression(campo, TextBox.TextProperty)?.UpdateSource();
            return;
        }

        if (string.IsNullOrEmpty(formato) || !LeituraNumerica.TentarLer(texto, out var valor))
            return;

        campo.Text = valor.ToString(formato, CulturaPtBr);
        BindingOperations.GetBindingExpression(campo, TextBox.TextProperty)?.UpdateSource();
    }

    // Zero em número ("0", "0,00") ou em horas ("0:00").
    private static bool EhZero(string texto)
    {
        if (LeituraNumerica.TentarLer(texto, out var valor))
            return valor == 0m;
        var partes = texto.Trim().Split(':');
        return partes.Length == 2 && int.TryParse(partes[0], out var horas) && int.TryParse(partes[1], out var minutos) && horas == 0 && minutos == 0;
    }
}
