using System.Globalization;
using CalculosTrabalhistasTributarios.Presentation;

namespace CalculosTrabalhistasTributarios.Mobile.Comportamentos;

public sealed class EntradaMonetariaBehavior : Behavior<Entry>
{
    protected override void OnAttachedTo(Entry entry)
    {
        base.OnAttachedTo(entry);
        entry.Focused += AoReceberFoco;
        entry.Unfocused += AoPerderFoco;
    }

    protected override void OnDetachingFrom(Entry entry)
    {
        entry.Focused -= AoReceberFoco;
        entry.Unfocused -= AoPerderFoco;
        base.OnDetachingFrom(entry);
    }

    private static void AoReceberFoco(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry && LeituraNumerica.TentarLer(entry.Text, out var valor) && valor == 0m)
            entry.Text = string.Empty;
    }

    private static void AoPerderFoco(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry)
            return;
        if (string.IsNullOrWhiteSpace(entry.Text))
            entry.Text = "0,00";
        else if (LeituraNumerica.TentarLer(entry.Text, out var valor) && decimal.Round(valor, 2) == valor)
            entry.Text = valor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
    }
}
