using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

namespace CalculosTrabalhistasTributarios.Mobile.Comportamentos;

public sealed class EntradaNumericaBehavior : Behavior<Entry>
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
        if (sender is Entry entry) entry.Text = EdicaoNumeroMobile.AoReceberFoco(entry.Text);
    }

    private static void AoPerderFoco(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry) entry.Text = EdicaoNumeroMobile.AoPerderFoco(entry.Text);
    }
}
