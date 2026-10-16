namespace CalculosTrabalhistasTributarios.Domain.Comum;

/// <summary>
/// Dias sem expediente bancário em todo o país, que adiam ou antecipam o vencimento das guias. Não são dias úteis os
/// sábados, os domingos, os feriados nacionais, a segunda e a terça-feira de Carnaval e o dia de Corpus Christi
/// (Resolução CMN 4.880/2020, art. 6º). Feriados nacionais: 1º/01, 21/04, 1º/05, 07/09, 02/11, 15/11 e 25/12 (Lei 662/1949,
/// na redação da Lei 10.607/2002), 12/10 (Lei 6.802/1980) e 20/11 desde 2024 (Lei 14.759/2023), além da Sexta-feira da
/// Paixão (Lei 9.093/1995, art. 2º), que os calendários de feriados bancários tratam como nacional. As datas móveis
/// partem da Páscoa. Feriados estaduais e municipais não são considerados.
/// </summary>
public static class CalendarioBancario
{
    private static readonly (int Mes, int Dia)[] FeriadosFixos = [(1, 1), (4, 21), (5, 1), (9, 7), (10, 12), (11, 2), (11, 15), (12, 25)];
    private const int PrimeiroAnoConscienciaNegra = 2024;

    public static bool DiaUtil(DateOnly dia) => dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Feriado(dia);

    /// <summary>Feriado nacional ou dia de Carnaval e de Corpus Christi, sem expediente bancário.</summary>
    public static bool Feriado(DateOnly dia)
    {
        if (FeriadosFixos.Contains((dia.Month, dia.Day)))
            return true;
        if (dia.Month == 11 && dia.Day == 20 && dia.Year >= PrimeiroAnoConscienciaNegra)
            return true;
        var pascoa = Pascoa(dia.Year);
        var distancia = dia.DayNumber - pascoa.DayNumber;
        // Segunda e terça de Carnaval (48 e 47 dias antes), Sexta-feira da Paixão (2 antes) e Corpus Christi (60 depois).
        return distancia is -48 or -47 or -2 or 60;
    }

    /// <summary>O próprio dia, se for útil, ou o primeiro dia útil depois dele.</summary>
    public static DateOnly ProximoDiaUtil(DateOnly dia)
    {
        while (!DiaUtil(dia))
            dia = dia.AddDays(1);
        return dia;
    }

    /// <summary>O próprio dia, se for útil, ou o último dia útil antes dele.</summary>
    public static DateOnly DiaUtilAnterior(DateOnly dia)
    {
        while (!DiaUtil(dia))
            dia = dia.AddDays(-1);
        return dia;
    }

    /// <summary>Domingo de Páscoa do calendário gregoriano (algoritmo de Meeus, Jones e Butcher).</summary>
    public static DateOnly Pascoa(int ano)
    {
        var a = ano % 19;
        var b = ano / 100;
        var c = ano % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(ano, mes, dia);
    }
}
