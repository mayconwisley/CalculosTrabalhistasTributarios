using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>Vencimentos de guias informados nos demonstrativos.</summary>
internal static class Prazos
{
    /// <summary>Último dia útil do mês, com os feriados nacionais do calendário bancário; feriados locais não são considerados.</summary>
    public static DateOnly UltimoDiaUtil(DateOnly mes) =>
        CalendarioBancario.DiaUtilAnterior(new DateOnly(mes.Year, mes.Month, DateTime.DaysInMonth(mes.Year, mes.Month)));
}
