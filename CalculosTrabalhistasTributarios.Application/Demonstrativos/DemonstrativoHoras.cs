using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>Verbas e fórmulas das horas extras, do adicional noturno e do DSR no demonstrativo.</summary>
internal static class DemonstrativoHoras
{
    /// <summary>Verbas das horas com valor, na ordem do demonstrativo.</summary>
    public static IEnumerable<VerbaDto> Proventos(HorasDoMes horas)
    {
        var h = horas.Informadas;
        if (horas.Faixa1 > 0m) yield return new($"Horas extras {Formato.PercentualCurto(h.PercentualFaixa1)}", Formato.Horas(h.HorasFaixa1), horas.Faixa1);
        if (horas.Faixa2 > 0m) yield return new($"Horas extras {Formato.PercentualCurto(h.PercentualFaixa2)}", Formato.Horas(h.HorasFaixa2), horas.Faixa2);
        if (horas.ExtrasNoturnas > 0m) yield return new($"Horas extras noturnas {Formato.PercentualCurto(h.PercentualFaixa1)}", Formato.Horas(horas.HorasExtrasNoturnasReduzidas), horas.ExtrasNoturnas);
        if (horas.AdicionalNoturno > 0m) yield return new($"Adicional noturno {Formato.PercentualCurto(h.PercentualNoturno)}", Formato.Horas(horas.HorasNoturnasReduzidas), horas.AdicionalNoturno);
        if (horas.Dsr > 0m) yield return new("DSR sobre horas extras e adicional noturno", Formato.Dias(horas.DiasDescanso), horas.Dsr);
    }

    /// <summary>Fórmulas das horas, do DSR e dos dias do mês; o valor da hora fica com quem chama, que conhece a sua base.</summary>
    public static IEnumerable<FormulaDto> Formulas(HorasDoMes horas)
    {
        var h = horas.Informadas;
        var valorHora = horas.ValorHora.ToString("C4", Formato.Cultura);
        var reducao = h.Rural ? "" : " x 8/7 (hora reduzida)";
        if (h.HorasFaixa1 > 0m)
            yield return new($"Horas extras {Formato.PercentualCurto(h.PercentualFaixa1)}", $"{valorHora} x {Formato.Percentual(100m + h.PercentualFaixa1)} x {Formato.Horas(h.HorasFaixa1)} = {Formato.Moeda(horas.Faixa1)}");
        if (h.HorasFaixa2 > 0m)
            yield return new($"Horas extras {Formato.PercentualCurto(h.PercentualFaixa2)}", $"{valorHora} x {Formato.Percentual(100m + h.PercentualFaixa2)} x {Formato.Horas(h.HorasFaixa2)} = {Formato.Moeda(horas.Faixa2)}");
        if (h.HorasExtrasNoturnas > 0m)
            yield return new($"Horas extras noturnas {Formato.PercentualCurto(h.PercentualFaixa1)}", $"{valorHora} x {Formato.Percentual(100m + h.PercentualNoturno)} (noturno) x {Formato.Percentual(100m + h.PercentualFaixa1)} x {Formato.Horas(h.HorasExtrasNoturnas)} de relógio{reducao} = {Formato.Moeda(horas.ExtrasNoturnas)}");
        if (h.HorasNoturnas > 0m)
        {
            if (!h.Rural)
                yield return new("Hora noturna reduzida", $"{Formato.Horas(h.HorasNoturnas)} de relógio x 60 ÷ 52,5 = {Formato.Horas(horas.HorasNoturnasReduzidas)} noturnas");
            yield return new("Adicional noturno", $"{valorHora} x {Formato.Percentual(h.PercentualNoturno)} x {Formato.Horas(h.HorasNoturnas)} de relógio{reducao} = {Formato.Moeda(horas.AdicionalNoturno)}");
        }
        yield return new("Dias do mês", $"{horas.DiasUteis} dias úteis e {horas.DiasDescanso} de descanso (domingos{(h.Feriados > 0 ? $" e {h.Feriados} feriado(s)" : "")})");
        if (horas.Variaveis > 0m)
            yield return new("DSR", $"{Formato.Moeda(horas.Variaveis)} (horas extras e adicional noturno) ÷ {horas.DiasUteis} dias úteis x {horas.DiasDescanso} dias de descanso = {Formato.Moeda(horas.Dsr)}");
    }
}
