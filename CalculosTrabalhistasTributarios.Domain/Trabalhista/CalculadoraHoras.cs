using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>
/// Horas extras, adicional noturno e o reflexo dos dois no descanso semanal remunerado (DSR), comuns à calculadora de
/// horas extras e ao holerite do mês.
/// </summary>
public static class CalculadoraHoras
{
    // CF, art. 7º, XVI: a hora extra vale pelo menos 50% a mais que a normal.
    public const decimal PercentualMinimoHoraExtra = 50m;

    // Adicional noturno mínimo: 20% no trabalho urbano (CLT, art. 73) e 25% no rural (Lei 5.889/1973, art. 7º).
    public const decimal PercentualMinimoNoturnoUrbano = 20m;
    public const decimal PercentualMinimoNoturnoRural = 25m;

    /// <summary>Falha com horas negativas, divisor zerado ou adicional abaixo do mínimo legal.</summary>
    public static Result Validar(HorasInformadas horas, decimal divisor)
    {
        if (horas.HorasFaixa1 < 0m || horas.HorasFaixa2 < 0m || horas.HorasNoturnas < 0m || horas.HorasExtrasNoturnas < 0m || horas.PercentualNoturno < 0m || horas.Feriados < 0)
            return Erro.Validacao("As horas, os percentuais e os feriados não podem ser negativos.");
        if (divisor <= 0m)
            return Erro.Validacao("O divisor de horas deve ser maior que zero, por exemplo 220 para 44 horas semanais.");
        // Só valida o adicional da faixa que tem horas: um campo sem horas não deve impedir o cálculo.
        if ((horas.HorasFaixa1 > 0m || horas.HorasExtrasNoturnas > 0m) && horas.PercentualFaixa1 < PercentualMinimoHoraExtra
            || horas.HorasFaixa2 > 0m && horas.PercentualFaixa2 < PercentualMinimoHoraExtra)
            return Erro.Validacao("O adicional de horas extras deve ser de pelo menos 50% (Constituição Federal, art. 7º, XVI).");
        if ((horas.HorasNoturnas > 0m || horas.HorasExtrasNoturnas > 0m) && horas.PercentualNoturno < (horas.Rural ? PercentualMinimoNoturnoRural : PercentualMinimoNoturnoUrbano))
            return Erro.Validacao(horas.Rural
                ? "O adicional noturno rural é de pelo menos 25% (Lei 5.889/1973, art. 7º, parágrafo único)."
                : "O adicional noturno urbano é de pelo menos 20% (CLT, art. 73).");
        return Result.Ok();
    }

    public static Result<HorasDoMes> Calcular(DateOnly competencia, decimal baseHora, decimal divisor, HorasInformadas h)
    {
        var dias = RegrasTrabalhistas.DiasParaDsr(competencia, h.Feriados);
        if (dias.Falhou)
            return dias.Erro;
        var (diasUteis, diasDescanso) = dias.Valor;

        // Os adicionais de natureza salarial integram o valor da hora (Súmula 264 e OJ 47 da SDI-1 do TST). As contas
        // multiplicam antes de dividir, para não perder o meio centavo no arredondamento.
        var faixa1 = CalculadoraTributacao.Arredondar(baseHora * (100m + h.PercentualFaixa1) * h.HorasFaixa1 / (divisor * 100m));
        var faixa2 = CalculadoraTributacao.Arredondar(baseHora * (100m + h.PercentualFaixa2) * h.HorasFaixa2 / (divisor * 100m));
        // A hora noturna urbana é reduzida: 7 horas de relógio valem 8; a rural não tem redução.
        var (horasPagas, horasDeRelogio) = h.Rural ? (1m, 1m) : (8m, 7m);
        var adicionalNoturno = CalculadoraTributacao.Arredondar(baseHora * h.PercentualNoturno * h.HorasNoturnas * horasPagas / (divisor * 100m * horasDeRelogio));
        // A hora extra noturna leva o adicional noturno na base do adicional de hora extra (OJ 97 da SDI-1).
        var extrasNoturnas = CalculadoraTributacao.Arredondar(baseHora * (100m + h.PercentualNoturno) * (100m + h.PercentualFaixa1) * h.HorasExtrasNoturnas * horasPagas / (divisor * 10_000m * horasDeRelogio));
        var dsr = CalculadoraTributacao.Arredondar((faixa1 + faixa2 + adicionalNoturno + extrasNoturnas) * diasDescanso / diasUteis);
        return new HorasDoMes(h, baseHora, divisor, faixa1, faixa2, adicionalNoturno, extrasNoturnas, dsr, diasUteis, diasDescanso);
    }
}
