using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>
/// Demonstrativo da jornada pelo ponto para quem só exibe <see cref="DemonstrativoDto"/>, como o aplicativo móvel. A
/// apuração é de horas, não de dinheiro: não há proventos nem descontos, e os totais vão no quadro comparativo. Usa os
/// mesmos indicadores, colunas da grade, critérios e observações da janela do desktop, sem refazer a apuração.
/// </summary>
public static class DemonstrativoJornada
{
    /// <param name="marcacoes">Marcações enviadas à apuração, para a memória mostrar as entradas e saídas de cada dia; opcional.</param>
    public static DemonstrativoDto Montar(SimulacaoJornadaDto apuracao, IReadOnlyList<MarcacaoDia>? marcacoes = null)
    {
        var t = apuracao.Totais;
        var destaques = new List<DestaqueDto>
        {
            new("Horas trabalhadas", Horas(t.Trabalhadas), $"Previstas: {Horas(t.Previstas)}"),
            new("Extras dos dias úteis", Horas(t.ExtrasFaixa1 + t.ExtrasNoturnas), t.ExtrasNoturnas > 0 ? $"{Horas(t.ExtrasNoturnas)} no período noturno" : "Adicional da faixa 1"),
            new("Extras em descanso e feriado", Horas(t.ExtrasFaixa2), "Adicional da faixa 2"),
            new("Horas noturnas", Horas(t.Noturnas + t.ExtrasNoturnas), "Horas de relógio"),
            new("Faltas e atrasos", t.Faltas == 1 ? "1 dia" : $"{t.Faltas} dias", $"{Horas(t.Faltantes)} de atrasos; {t.DescansosPerdidos} DSR perdido(s)"),
            new("Intervalos suprimidos", Horas(t.IntervaloSuprimido + t.InterjornadaSuprimida), $"Intrajornada {Horas(t.IntervaloSuprimido)} • entre jornadas {Horas(t.InterjornadaSuprimida)}")
        };

        // Os mesmos totais que o desktop leva para as horas extras e o holerite, para o usuário informá-los lá.
        var totais = new TabelaComparativaDto("Totais do período", ["Total"],
        [
            new("Horas trabalhadas", [Horas(t.Trabalhadas)]),
            new("Horas previstas", [Horas(t.Previstas)]),
            new("Horas extras (faixa 1): dias úteis, fora do período noturno", [Horas(t.ExtrasFaixa1)], true),
            new("Horas extras (faixa 2): descansos e feriados", [Horas(t.ExtrasFaixa2)], true),
            new("Horas noturnas (relógio), fora das extras de dias úteis", [Horas(t.Noturnas)]),
            new("Horas extras noturnas (relógio) de dias úteis", [Horas(t.ExtrasNoturnas)]),
            new("Faltas (dias)", [t.Faltas.ToString(Formato.Cultura)]),
            new("Descansos semanais perdidos", [t.DescansosPerdidos.ToString(Formato.Cultura)]),
            new("Atrasos e saídas antecipadas", [Horas(t.Faltantes)]),
            new("Intervalo intrajornada suprimido", [Horas(t.IntervaloSuprimido)]),
            new("Intervalo entre jornadas suprimido", [Horas(t.InterjornadaSuprimida)]),
            new("Feriados fora do domingo", [t.Feriados.ToString(Formato.Cultura)])
        ]);

        var porData = marcacoes?.GroupBy(dia => dia.Data).ToDictionary(grupo => grupo.Key, grupo => grupo.First());
        var formulas = apuracao.Dias
            .Select(dia => new FormulaDto($"{dia.Data.ToString("dd/MM ddd", Formato.Cultura)} • {NomeTipo(dia.Tipo)}", Descrever(dia, porData?.GetValueOrDefault(dia.Data))))
            .ToList();
        var trabalhados = apuracao.Dias.Count(dia => dia.Trabalhado);
        var memoria = new List<GrupoMemoriaDto>
        {
            new("Dias do ponto", $"{apuracao.Dias.Count} dia(s) • {trabalhados} com trabalho", formulas)
        };

        var referencia = apuracao.Dias.Count == 0
            ? $"Competência {Formato.Competencia(apuracao.Competencia)}"
            : $"Competência {Formato.Competencia(apuracao.Competencia)} • ponto de {Formato.Data(apuracao.Dias.Min(dia => dia.Data))} a {Formato.Data(apuracao.Dias.Max(dia => dia.Data))}";

        return new DemonstrativoDto(
            "Jornada pelo ponto",
            referencia,
            destaques,
            [],
            [],
            [],
            memoria,
            [.. apuracao.Criterios, .. apuracao.Observacoes],
            RotuloResultado: "Horas apuradas",
            Comparativo: totais);
    }

    /// <summary>As colunas de resultado da grade de dias da janela: trabalhadas, extras, noturnas, faltas e intervalos.</summary>
    private static string Descrever(DiaJornadaDto dia, MarcacaoDia? marcacao)
    {
        var partes = new List<string> { $"prevista {Horas(dia.MinutosPrevistos)}" };
        if (marcacao is { Periodos.Count: > 0 })
            partes.Add("marcações " + string.Join(", ", marcacao.Periodos.Select(periodo => $"{periodo.Entrada:HH\\:mm}–{periodo.Saida:HH\\:mm}")));
        if (dia.Falta)
            partes.Add("falta");
        else if (dia.Trabalhado)
            partes.Add($"trabalhadas {Horas(dia.MinutosTrabalhados)}");
        if (dia.MinutosExtras > 0)
            partes.Add($"extras {Horas(dia.MinutosExtras)}" + (dia.ExtraComAdicionalDeDescanso ? " (100%)" : ""));
        if (dia.MinutosNoturnos > 0)
            partes.Add($"noturnas {Horas(dia.MinutosNoturnos)}");
        if (!dia.Falta && dia.MinutosFaltantes > 0)
            partes.Add($"atrasos {Horas(dia.MinutosFaltantes)}");
        if (dia.IntervaloSuprimido > 0)
            partes.Add($"{Horas(dia.IntervaloSuprimido)} de intrajornada suprimida");
        if (dia.InterjornadaSuprimida > 0)
            partes.Add($"{Horas(dia.InterjornadaSuprimida)} entre jornadas suprimidas");
        if (!dia.Trabalhado && !dia.Falta && dia.MinutosPrevistos == 0)
            partes.Add("sem trabalho");
        return string.Join(" • ", partes);
    }

    private static string NomeTipo(TipoDia tipo) => tipo switch
    {
        TipoDia.Descanso => "Descanso",
        TipoDia.Feriado => "Feriado",
        _ => "Útil"
    };

    /// <summary>Minutos no formato de relógio da janela, como 8:30.</summary>
    private static string Horas(int minutos) => $"{minutos / 60}:{minutos % 60:00}";
}
