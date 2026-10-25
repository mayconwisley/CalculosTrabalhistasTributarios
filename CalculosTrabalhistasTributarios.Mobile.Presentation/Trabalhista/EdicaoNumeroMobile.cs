using CalculosTrabalhistasTributarios.Presentation;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public static class EdicaoNumeroMobile
{
    public static string AoReceberFoco(string? texto) =>
        LeituraNumerica.TentarLer(texto, out var valor) && valor == 0m ? string.Empty : texto ?? string.Empty;

    public static string AoPerderFoco(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? "0" : texto;
}
