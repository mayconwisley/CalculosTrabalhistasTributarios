namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>Vencimentos de guias informados nos demonstrativos.</summary>
internal static class Prazos
{
    /// <summary>Último dia útil do mês, sem considerar feriados, que o usuário confere no calendário local.</summary>
    public static DateOnly UltimoDiaUtil(DateOnly mes)
    {
        var dia = new DateOnly(mes.Year, mes.Month, DateTime.DaysInMonth(mes.Year, mes.Month));
        while (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            dia = dia.AddDays(-1);
        return dia;
    }
}
