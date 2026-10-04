using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using Xunit;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Tests;

public class JornadaTests
{
    private static readonly DateOnly Outubro = new(2026, 10, 1);

    private static MarcacaoDia Dia(int dia, string marcacoes, int previstos = 8 * 60, TipoDia tipo = TipoDia.Util) =>
        new(new DateOnly(2026, 10, dia), tipo, previstos, marcacoes.Length == 0 ? [] : marcacoes.Split(' ')
            .Select(periodo => periodo.Split('-'))
            .Select(horas => new PeriodoTrabalhado(TimeOnly.Parse(horas[0]), TimeOnly.Parse(horas[1])))
            .ToArray());

    private static Result<SimulacaoJornadaDto> Resultado(TrabalhoNoturno noturno, params MarcacaoDia[] dias) =>
        new ApurarJornadaUseCase().Apurar(new ApurarJornadaRequest(Outubro, noturno, dias));

    private static SimulacaoJornadaDto Apurar(TrabalhoNoturno noturno, params MarcacaoDia[] dias) => Resultado(noturno, dias).Sucesso();

    private static DiaJornadaDto Um(string marcacoes, int previstos = 8 * 60, TipoDia tipo = TipoDia.Util, TrabalhoNoturno noturno = TrabalhoNoturno.Urbano) =>
        Apurar(noturno, Dia(5, marcacoes, previstos, tipo)).Dias.Single();

    [Fact]
    public void Jornada_normal_sem_extras()
    {
        var dia = Um("08:00-12:00 13:00-17:00");
        Assert.Equal(480, dia.MinutosTrabalhados);
        Assert.Equal(0, dia.MinutosExtras + dia.MinutosNoturnos + dia.MinutosFaltantes + dia.IntervaloSuprimido);
    }

    [Theory]
    [InlineData("08:00-12:00 13:00-17:10", 0, 0)]    // 10 minutos a mais: tolerância
    [InlineData("08:00-12:00 13:00-17:11", 11, 0)]   // passou da tolerância: conta tudo (Súmula 366)
    [InlineData("08:10-12:00 13:00-17:00", 0, 0)]    // 10 minutos a menos: tolerância
    [InlineData("08:30-12:00 13:00-17:00", 0, 30)]   // atraso de 30 minutos
    [InlineData("08:00-12:00 13:00-19:00", 120, 0)]
    public void Extras_e_atrasos_com_a_tolerancia_de_10_minutos(string marcacoes, int extras, int faltantes)
    {
        var dia = Um(marcacoes);
        Assert.Equal(extras, dia.MinutosExtras);
        Assert.Equal(faltantes, dia.MinutosFaltantes);
    }

    [Fact]
    public void Extras_sao_os_ultimos_minutos_e_a_parte_noturna_e_separada()
    {
        // 14h às 18h e 19h às 23h30: 8h30 trabalhadas, 30 minutos extras (23h às 23h30), 1h30 noturna urbana (22h às 23h30).
        var dia = Um("14:00-18:00 19:00-23:30");
        Assert.Equal(30, dia.MinutosExtras);
        Assert.Equal(90, dia.MinutosNoturnos);
        Assert.Equal(30, dia.MinutosNoturnosExtras);
    }

    [Fact]
    public void Prorrogacao_depois_da_noite_inteira_continua_noturna()
    {
        // 22h às 2h e 3h às 7h: 6 horas no período noturno e mais 2 depois das 5h (Súmula 60, II).
        var inteira = Um("22:00-02:00 03:00-07:00");
        Assert.Equal(480, inteira.MinutosTrabalhados);
        Assert.Equal(480, inteira.MinutosNoturnos);
        // Começando às 23h, a noite não foi cumprida inteira: só até as 5h é noturno.
        var parcial = Um("23:00-03:00 04:00-07:00");
        Assert.Equal(4 * 60 + 60, parcial.MinutosNoturnos);
    }

    [Theory]
    [InlineData(TrabalhoNoturno.Urbano, 60)]          // 22h às 23h
    [InlineData(TrabalhoNoturno.RuralLavoura, 120)]   // 21h às 23h
    [InlineData(TrabalhoNoturno.RuralPecuaria, 180)]  // 20h às 23h
    public void Periodo_noturno_urbano_e_rural(TrabalhoNoturno noturno, int minutosNoturnos) =>
        Assert.Equal(minutosNoturnos, Um("15:00-19:00 19:30-23:00", noturno: noturno).MinutosNoturnos);

    [Fact]
    public void Intervalo_menor_que_uma_hora_acima_de_6_horas()
    {
        var dia = Um("08:00-12:00 12:30-17:00");
        Assert.Equal(30, dia.IntervaloSuprimido);
        Assert.Equal(15, Um("08:00-13:00", previstos: 5 * 60).IntervaloSuprimido);   // acima de 4 e até 6 horas: 15 minutos
        Assert.Equal(0, Um("08:00-12:00", previstos: 4 * 60).IntervaloSuprimido);
    }

    [Fact]
    public void Menos_de_11_horas_entre_jornadas()
    {
        // Saída às 23h e volta às 7h: 8 horas de descanso, faltaram 3.
        var apuracao = Apurar(TrabalhoNoturno.Urbano, Dia(5, "13:00-17:00 18:00-23:00"), Dia(6, "07:00-11:00 12:00-16:00"));
        Assert.Equal(180, apuracao.Dias[1].InterjornadaSuprimida);
        Assert.Equal(180, apuracao.Totais.InterjornadaSuprimida);
    }

    [Fact]
    public void Faltas_descansos_perdidos_e_trabalho_no_domingo()
    {
        // 05/10 e 07/10 na mesma semana, 13/10 em outra: 3 faltas e 2 descansos perdidos. Domingo 11/10 trabalhado: faixa 2.
        var apuracao = Apurar(TrabalhoNoturno.Urbano,
            Dia(5, ""), Dia(6, "08:00-12:00 13:00-17:00"), Dia(7, ""), Dia(11, "08:00-12:00", 0, TipoDia.Descanso), Dia(12, "", 0, TipoDia.Feriado), Dia(13, ""));
        Assert.Equal(3, apuracao.Totais.Faltas);
        Assert.Equal(2, apuracao.Totais.DescansosPerdidos);
        Assert.Equal(240, apuracao.Totais.ExtrasFaixa2);
        Assert.Equal(1, apuracao.Totais.Feriados);
    }

    [Fact]
    public void Totais_separam_extras_noturnas_das_diurnas()
    {
        var apuracao = Apurar(TrabalhoNoturno.Urbano, Dia(5, "14:00-18:00 19:00-23:30"), Dia(6, "08:00-12:00 13:00-19:00"));
        Assert.Equal(120, apuracao.Totais.ExtrasFaixa1);    // as 2 horas extras diurnas do dia 6
        Assert.Equal(30, apuracao.Totais.ExtrasNoturnas);   // a meia hora extra noturna do dia 5
        Assert.Equal(60, apuracao.Totais.Noturnas);         // a hora noturna normal do dia 5
    }

    [Fact]
    public void Barra_marcacoes_que_passam_de_24_horas_ou_comecam_antes_da_jornada_anterior()
    {
        Resultado(TrabalhoNoturno.Urbano, Dia(5, "08:00-20:00 21:00-09:00")).Falha();
        Resultado(TrabalhoNoturno.Urbano, Dia(5, "20:00-06:00"), Dia(6, "05:00-08:00")).Falha();
    }

    [Theory]
    [InlineData("08:00", 8, 0)]
    [InlineData("8", 8, 0)]
    [InlineData("8h30", 8, 30)]
    [InlineData("0745", 7, 45)]
    [InlineData("17:48", 17, 48)]
    public void Le_horarios_digitados_de_varias_formas(string texto, int horas, int minutos)
    {
        Assert.True(LeituraDeHoras.TentarLerHorario(texto, out var horario));
        Assert.Equal(new TimeOnly(horas, minutos), horario);
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("8:75")]
    [InlineData("oito")]
    public void Recusa_horarios_invalidos(string texto) => Assert.False(LeituraDeHoras.TentarLerHorario(texto, out _));
}
