using System.Globalization;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Horários de relógio e durações digitados de várias formas: 08:00, 8:00, 8h, 8h30, 0800 ou 8.</summary>
public static class LeituraDeHoras
{
    public static bool TentarLerHorario(string texto, out TimeOnly horario)
    {
        horario = default;
        if (!TentarLerMinutos(texto, out var minutos) || minutos >= 24 * 60)
            return false;
        horario = new TimeOnly(minutos / 60, minutos % 60);
        return true;
    }

    /// <summary>Duração vazia vale zero.</summary>
    public static bool TentarLerDuracao(string texto, out int minutos)
    {
        minutos = 0;
        return string.IsNullOrWhiteSpace(texto) || TentarLerMinutos(texto, out minutos);
    }

    private static bool TentarLerMinutos(string texto, out int minutos)
    {
        minutos = 0;
        var limpo = texto.Trim().ToLowerInvariant().Replace('h', ':').TrimEnd(':');
        string horas, resto;
        if (limpo.Contains(':'))
        {
            var partes = limpo.Split(':');
            if (partes.Length != 2)
                return false;
            (horas, resto) = (partes[0], partes[1].Length == 0 ? "0" : partes[1]);
        }
        else if (limpo.Length is 3 or 4)
            (horas, resto) = (limpo[..^2], limpo[^2..]);
        else
            (horas, resto) = (limpo, "0");
        if (!int.TryParse(horas, NumberStyles.None, CultureInfo.InvariantCulture, out var h) || !int.TryParse(resto, NumberStyles.None, CultureInfo.InvariantCulture, out var m) || m >= 60)
            return false;
        minutos = h * 60 + m;
        return true;
    }
}
