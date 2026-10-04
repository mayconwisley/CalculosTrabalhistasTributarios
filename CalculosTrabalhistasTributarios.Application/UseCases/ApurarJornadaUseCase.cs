using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Apuração do cartão de ponto: as marcações de cada dia viram horas normais, extras, noturnas, faltas e atrasos, além
/// dos intervalos não concedidos. Cada dia é uma linha do tempo em minutos, a partir da meia-noite do dia da primeira
/// entrada; uma jornada que passa da meia-noite continua no mesmo dia.
/// </summary>
public sealed class ApurarJornadaUseCase : IApurarJornadaUseCase
{
    private const int MinutosNoDia = 24 * 60;

    // Variação de até 10 minutos no dia não conta como extra nem como atraso (CLT, art. 58, § 1º, e Súmula 366 do TST).
    private const int Tolerancia = 10;

    // Intervalo mínimo entre duas jornadas (CLT, art. 66).
    private const int Interjornada = 11 * 60;
    private const int JornadaPrevistaMaxima = 12 * 60;

    public Result<SimulacaoJornadaDto> Apurar(ApurarJornadaRequest r)
    {
        if (r.Dias.Count == 0)
            return Erro.Validacao("Gere os dias do mês e informe as marcações antes de apurar.");
        var (inicioNoite, fimNoite, nomeNoite) = r.Noturno switch
        {
            TrabalhoNoturno.RuralLavoura => (21 * 60, 5 * 60, "das 21h às 5h (rural, na lavoura)"),
            TrabalhoNoturno.RuralPecuaria => (20 * 60, 4 * 60, "das 20h às 4h (rural, na pecuária)"),
            _ => (22 * 60, 5 * 60, "das 22h às 5h (urbano)")
        };

        var dias = new List<DiaJornadaDto>();
        DateTime? ultimaSaida = null;
        foreach (var dia in r.Dias.OrderBy(dia => dia.Data))
        {
            if (dia.MinutosPrevistos is < 0 or > JornadaPrevistaMaxima)
                return Erro.Validacao($"A jornada prevista de {dia.Data:dd/MM} deve ficar entre 0 e 12 horas.");
            var linhaDoTempo = LinhaDoTempo(dia);
            if (linhaDoTempo.Falhou)
                return linhaDoTempo.Erro;
            var periodos = linhaDoTempo.Valor;
            var trabalhados = periodos.Sum(periodo => periodo.Fim - periodo.Inicio);
            var previstos = dia.Tipo == TipoDia.Util ? dia.MinutosPrevistos : 0;
            var diferenca = trabalhados - previstos;
            var extras = dia.Tipo != TipoDia.Util ? trabalhados : diferenca > Tolerancia ? diferenca : 0;
            var falta = dia.Tipo == TipoDia.Util && previstos > 0 && trabalhados == 0;
            var faltantes = dia.Tipo == TipoDia.Util && trabalhados > 0 && -diferenca > Tolerancia ? -diferenca : 0;
            var (noturnos, noturnosExtras) = Noturnos(periodos, extras, inicioNoite, fimNoite);

            var interjornada = 0;
            if (periodos.Count > 0)
            {
                var entrada = dia.Data.ToDateTime(TimeOnly.MinValue).AddMinutes(periodos[0].Inicio);
                if (ultimaSaida is { } saida)
                {
                    var descanso = (int)(entrada - saida).TotalMinutes;
                    if (descanso < 0)
                        return Erro.Validacao($"As marcações de {dia.Data:dd/MM} começam antes do fim da jornada anterior.");
                    interjornada = Math.Max(0, Interjornada - descanso);
                }
                ultimaSaida = dia.Data.ToDateTime(TimeOnly.MinValue).AddMinutes(periodos[^1].Fim);
            }

            dias.Add(new DiaJornadaDto(dia.Data, dia.Tipo, previstos, trabalhados, extras, noturnos, noturnosExtras, faltantes, falta, Intervalo(periodos, trabalhados), interjornada));
        }

        var totais = Totalizar(dias);
        return new SimulacaoJornadaDto(r.Competencia, r.Noturno, dias, totais, Criterios(nomeNoite, r.Noturno), Observacoes(totais, r.Noturno));
    }

    /// <summary>Períodos do dia em minutos desde a meia-noite, em ordem; o que passa da meia-noite soma 24 horas.</summary>
    private static Result<List<(int Inicio, int Fim)>> LinhaDoTempo(MarcacaoDia dia)
    {
        var periodos = new List<(int Inicio, int Fim)>();
        var deslocamento = 0;
        foreach (var periodo in dia.Periodos)
        {
            if (periodo.Entrada == periodo.Saida)
                return Erro.Validacao($"Em {dia.Data:dd/MM}, a entrada e a saída das {periodo.Entrada:HH\\:mm} são iguais.");
            var inicio = periodo.Entrada.Hour * 60 + periodo.Entrada.Minute + deslocamento;
            if (periodos.Count > 0 && inicio < periodos[^1].Fim)
            {
                // A entrada antes da saída anterior é do dia seguinte.
                deslocamento += MinutosNoDia;
                inicio += MinutosNoDia;
            }
            var fim = periodo.Saida.Hour * 60 + periodo.Saida.Minute + deslocamento;
            if (fim <= inicio)
            {
                deslocamento += MinutosNoDia;
                fim += MinutosNoDia;
            }
            periodos.Add((inicio, fim));
        }
        if (periodos.Count > 0 && periodos[^1].Fim - periodos[0].Inicio > MinutosNoDia)
            return Erro.Validacao($"As marcações de {dia.Data:dd/MM} passam de 24 horas; confira a ordem das entradas e saídas.");
        return periodos;
    }

    /// <summary>
    /// Minutos noturnos do dia e a parte deles que é extra. As extras são os últimos minutos trabalhados. Quem cumpre todo o
    /// período noturno e continua trabalhando tem as horas seguintes também como noturnas (Súmula 60, II, do TST).
    /// </summary>
    private static (int Noturnos, int Extras) Noturnos(List<(int Inicio, int Fim)> periodos, int extras, int inicioNoite, int fimNoite)
    {
        if (periodos.Count == 0)
            return (0, 0);
        // A noite que termina na madrugada do próprio dia e as que começam à noite.
        var janelas = Enumerable.Range(-1, 3).Select(dia => (Inicio: dia * MinutosNoDia + inicioNoite, Fim: (dia + 1) * MinutosNoDia + fimNoite)).ToArray();
        var prorrogacaoDesde = janelas
            .Where(janela => periodos[0].Inicio <= janela.Inicio && periodos.Any(periodo => periodo.Inicio < janela.Fim && periodo.Fim > janela.Fim))
            .Select(janela => (int?)janela.Fim)
            .FirstOrDefault();

        var normais = periodos.Sum(periodo => periodo.Fim - periodo.Inicio) - extras;
        var noturnos = 0;
        var noturnosExtras = 0;
        var trabalhados = 0;
        foreach (var (inicio, fim) in periodos)
        {
            for (var minuto = inicio; minuto < fim; minuto++, trabalhados++)
            {
                var noturno = janelas.Any(janela => minuto >= janela.Inicio && minuto < janela.Fim) || minuto >= prorrogacaoDesde;
                if (!noturno)
                    continue;
                noturnos++;
                if (trabalhados >= normais)
                    noturnosExtras++;
            }
        }
        return (noturnos, noturnosExtras);
    }

    /// <summary>
    /// Intervalo não concedido: acima de 6 horas de trabalho, o mínimo é de 1 hora; acima de 4 e até 6 horas, de 15 minutos
    /// (CLT, art. 71). O intervalo concedido é a soma das pausas entre os períodos.
    /// </summary>
    private static int Intervalo(List<(int Inicio, int Fim)> periodos, int trabalhados)
    {
        if (trabalhados <= 4 * 60)
            return 0;
        var minimo = trabalhados > 6 * 60 ? 60 : 15;
        var concedido = 0;
        for (var i = 1; i < periodos.Count; i++)
            concedido += periodos[i].Inicio - periodos[i - 1].Fim;
        return Math.Max(0, minimo - concedido);
    }

    private static TotaisJornadaDto Totalizar(IReadOnlyList<DiaJornadaDto> dias)
    {
        var uteis = dias.Where(dia => dia.Tipo == TipoDia.Util).ToArray();
        var descansos = dias.Where(dia => dia.Tipo != TipoDia.Util).ToArray();
        // Cada semana, de segunda a domingo, com falta injustificada perde o seu descanso remunerado.
        var semanasComFalta = dias.Where(dia => dia.Falta).Select(dia => dia.Data.AddDays(-(((int)dia.Data.DayOfWeek + 6) % 7))).Distinct().Count();
        return new TotaisJornadaDto(
            Trabalhadas: dias.Sum(dia => dia.MinutosTrabalhados),
            Previstas: dias.Sum(dia => dia.MinutosPrevistos),
            ExtrasFaixa1: uteis.Sum(dia => dia.MinutosExtras - dia.MinutosNoturnosExtras),
            ExtrasFaixa2: descansos.Sum(dia => dia.MinutosExtras),
            Noturnas: uteis.Sum(dia => dia.MinutosNoturnos - dia.MinutosNoturnosExtras) + descansos.Sum(dia => dia.MinutosNoturnos),
            ExtrasNoturnas: uteis.Sum(dia => dia.MinutosNoturnosExtras),
            Faltas: dias.Count(dia => dia.Falta),
            DescansosPerdidos: semanasComFalta,
            Faltantes: dias.Sum(dia => dia.MinutosFaltantes),
            IntervaloSuprimido: dias.Sum(dia => dia.IntervaloSuprimido),
            InterjornadaSuprimida: dias.Sum(dia => dia.InterjornadaSuprimida),
            Feriados: dias.Count(dia => dia.Tipo == TipoDia.Feriado && dia.Data.DayOfWeek != DayOfWeek.Sunday));
    }

    private static IReadOnlyList<string> Criterios(string nomeNoite, TrabalhoNoturno noturno) =>
    [
        "Horas extras: o que passa da jornada prevista do dia útil, contado nos últimos minutos trabalhados. Variações de até 10 minutos no dia não contam como extra nem como atraso (CLT, art. 58, § 1º, e Súmula 366 do TST).",
        "Descansos e feriados: todo o trabalho nesses dias é hora extra com o adicional de domingos e feriados (faixa 2 da calculadora de horas extras).",
        $"Período noturno {nomeNoite}. Quem cumpre todo o período noturno e continua trabalhando tem as horas seguintes também como noturnas (Súmula 60, II, do TST)."
            + (noturno == TrabalhoNoturno.Urbano ? " As horas são de relógio; a calculadora de horas extras faz a conversão para a hora noturna reduzida." : " No trabalho rural não há hora reduzida."),
        "Faltas: dia útil com jornada prevista e sem marcações. Cada semana com falta injustificada perde o descanso remunerado (Lei 605/1949, art. 6º).",
        "Intervalo para descanso e alimentação: pelo menos 1 hora acima de 6 horas de trabalho e 15 minutos acima de 4 horas (CLT, art. 71), somando as pausas do dia.",
        "Intervalo entre jornadas: pelo menos 11 horas entre a última saída de um dia e a primeira entrada do seguinte (CLT, art. 66)."
    ];

    private static IReadOnlyList<string> Observacoes(TotaisJornadaDto totais, TrabalhoNoturno noturno)
    {
        var observacoes = new List<string>();
        if (totais.IntervaloSuprimido > 0)
            observacoes.Add("O intervalo de descanso não concedido é pago como indenização, só pelo tempo suprimido, com acréscimo de 50% sobre a hora normal (CLT, art. 71, § 4º). Por ser indenização, não entra no INSS, no IRRF nem no FGTS e não foi somado às horas extras.");
        if (totais.InterjornadaSuprimida > 0)
            observacoes.Add("As horas que faltaram para as 11 horas entre jornadas são pagas como extras, com o adicional de 50%, mesmo que já tenham sido contadas como extras do dia (OJ 355 da SDI-1 do TST). Elas não foram somadas às horas extras: inclua-as na faixa 1 se forem devidas.");
        if (totais.ExtrasFaixa2 > 0 && totais.Noturnas > 0)
            observacoes.Add("As horas noturnas de descansos e feriados entram nas horas noturnas, para o adicional noturno, e nas extras da faixa 2, pelo valor da hora com o adicional de domingos e feriados.");
        if (noturno != TrabalhoNoturno.Urbano)
            observacoes.Add("Na calculadora de horas extras e no holerite, escolha o trabalho noturno rural, com adicional de 25% e sem hora reduzida.");
        return observacoes;
    }
}
