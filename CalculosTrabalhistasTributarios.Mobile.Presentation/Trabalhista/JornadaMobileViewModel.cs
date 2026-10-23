using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>
/// Jornada pelas marcações de ponto, como a janela do desktop: gera os dias do período do ponto pela escala e pelo
/// horário padrão, recebe as marcações de cada dia e apura horas extras, noturnas, faltas, atrasos e intervalos.
/// </summary>
public sealed class JornadaMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private const int Semanal = 0;
    private const int SeisPorUm = 1;
    private const int DozePorTrintaESeis = 2;
    private readonly IApurarJornadaUseCase _apuracao;
    private readonly INavegacaoTransferenciasMobile? _transferencias;
    private SimulacaoJornadaDto? _ultimaApuracao;
    public override bool PodeTransferirJornada => Demonstrativo is not null && _ultimaApuracao is not null && _transferencias is not null;
    public override ICommand JornadaParaHoleriteCommand { get; }
    public override ICommand JornadaParaHorasExtrasCommand { get; }
    private DateOnly _ultimaCompetencia;

    public override string Titulo => "Jornada pelo ponto";
    public override string Descricao => "Apure horas extras, noturnas, faltas, atrasos e intervalos pelas marcações de entrada e saída do período do ponto.";

    public JornadaMobileViewModel(IApurarJornadaUseCase apuracao, ICompartilhamentoPdf compartilhamento, ILogger<JornadaMobileViewModel> logger,
        INavegacaoTransferenciasMobile? transferencias = null)
        : base(compartilhamento, logger)
    {
        _apuracao = apuracao;
        _transferencias = transferencias;
        JornadaParaHoleriteCommand = new AsyncRelayCommand(() => TransferirJornadaAsync(TipoCalculadoraMobile.Holerite));
        JornadaParaHorasExtrasCommand = new AsyncRelayCommand(() => TransferirJornadaAsync(TipoCalculadoraMobile.HorasExtras));
        _ultimaCompetencia = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var (inicio, fim) = MesInteiro(_ultimaCompetencia);
        Secao("periodo", "Período do ponto");
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, _ultimaCompetencia.ToString("MM/yyyy", Cultura),
            "Mês da folha em que as horas são pagas. Ao mudar, o período que era o mês inteiro acompanha.");
        Adicionar("inicio", "Início do ponto (dd/MM/yyyy)", TipoCampoMobile.Data, inicio, "Para um ponto de 16/09 a 15/10, informe 16/09.");
        Adicionar("fim", "Fim do ponto (dd/MM/yyyy)", TipoCampoMobile.Data, fim, $"Até {ApurarJornadaRequest.MaximoDias} dias no período.");
        Adicionar("noturno", "Trabalho noturno", TipoCampoMobile.Opcao,
            dica: "Urbano das 22h às 5h, com hora reduzida; rural das 21h às 5h na lavoura e das 20h às 4h na pecuária, sem redução.",
            opcoes: ["Urbano (22h às 5h)", "Rural, lavoura (21h às 5h)", "Rural, pecuária (20h às 4h)"]);
        Secao("escalaSecao", "Escala e horário padrão");
        Adicionar("escala", "Escala", TipoCampoMobile.Opcao,
            dica: "Semanal: seg. a sex. com a jornada do dia útil, sábado com a jornada de sábado e domingo de descanso. 6x1: seis dias de trabalho e um de folga. 12x36: um dia de trabalho e um de descanso, com os feriados da escala compensados (CLT, art. 59-A).",
            opcoes: ["Semanal: seg. a sex. e sábado", "6x1: seis dias de trabalho e uma folga", "12x36: um dia de trabalho e um de descanso"]);
        Adicionar("inicioEscala", "1º dia de trabalho da escala (opcional)", TipoCampoMobile.Data,
            dica: "Um dia de trabalho que inicia o ciclo (dd/MM/yyyy). Em branco, o ciclo começa no início do ponto.");
        Adicionar("jornadaSemana", "Jornada do dia útil (h)", TipoCampoMobile.Horas, "8:00",
            "Horas previstas por dia, como 8:00 ou 8:48: de seg. a sex. na escala semanal; nas escalas 6x1 e 12x36, cada dia de trabalho, sem o intervalo.");
        Adicionar("jornadaSabado", "Jornada de sábado (h)", TipoCampoMobile.Horas, "4:00", "0:00 quando o sábado é compensado durante a semana.");
        Adicionar("entrada1", "Entrada", TipoCampoMobile.Horas, "08:00", "Horário padrão dos dias de trabalho gerados; ajuste os dias diferentes na grade.");
        Adicionar("saida1", "Saída para o intervalo", TipoCampoMobile.Horas, "12:00");
        Adicionar("entrada2", "Volta do intervalo", TipoCampoMobile.Horas, "13:00");
        Adicionar("saida2", "Saída", TipoCampoMobile.Horas, "17:00");
        var dias = AdicionarGrade("dias", "Marcações",
            "Gere os dias e ajuste o tipo e as marcações; horários depois da meia-noite continuam no mesmo dia. Sem dias, Calcular gera e apura direto.",
            "Dia", ApurarJornadaRequest.MaximoDias,
            [
                new ColunaGradeMobile("data", "Data (dd/MM/yyyy)", TipoCampoMobile.Data),
                new ColunaGradeMobile("tipo", "Tipo do dia", TipoCampoMobile.Opcao, Opcoes: ["Útil", "Descanso", "Feriado"]),
                new ColunaGradeMobile("previsto", "Jornada prevista (h)", TipoCampoMobile.Horas, "0:00"),
                new ColunaGradeMobile("entrada1", "Entrada", TipoCampoMobile.Horas),
                new ColunaGradeMobile("saida1", "Saída para o intervalo", TipoCampoMobile.Horas),
                new ColunaGradeMobile("entrada2", "Volta do intervalo", TipoCampoMobile.Horas),
                new ColunaGradeMobile("saida2", "Saída", TipoCampoMobile.Horas)
            ]);
        dias.RotuloGerar = "Gerar dias";
        dias.GerarCommand = new RelayCommand(GerarDias);
        AtualizarEscala();
    }

    private bool EscalaPorCiclo => Campo("escala").Indice != Semanal;

    protected override void AoAlterar(CampoMobile campo)
    {
        switch (campo.Chave)
        {
            case "escala":
                AtualizarEscala();
                break;
            case "competencia":
                AcompanharCompetencia();
                break;
        }
    }

    private void AtualizarEscala()
    {
        Campo("inicioEscala").Visivel = EscalaPorCiclo;
        Campo("jornadaSabado").Visivel = !EscalaPorCiclo;
    }

    /// <summary>
    /// Como no desktop: o período que era o mês inteiro da última competência válida passa para o novo mês. Compara com a
    /// última competência válida porque, ao digitar, o campo passa por valores incompletos, como "11/202".
    /// </summary>
    private void AcompanharCompetencia()
    {
        if (LerCompetencia(Campo("competencia").Valor) is not { } novo)
            return;
        if ((Texto("inicio"), Texto("fim")) == MesInteiro(_ultimaCompetencia))
            (Campo("inicio").Valor, Campo("fim").Valor) = MesInteiro(novo);
        _ultimaCompetencia = novo;
    }

    private void GerarDias()
    {
        Mensagem = null;
        try { GerarDiasDoPeriodo(); }
        catch (EntradaInvalidaException erro) { Mensagem = erro.Message; }
    }

    /// <summary>
    /// Cria as linhas dos dias que faltam no período, com o tipo e o horário padrão da escala. Diferente do desktop, que
    /// substitui a grade depois de confirmar, os dias já existentes mantêm o tipo e as marcações digitadas.
    /// </summary>
    private void GerarDiasDoPeriodo()
    {
        Competencia("competencia");
        var (inicio, fim) = LerPeriodo();
        var inicioCiclo = EscalaPorCiclo && DataOpcional("inicioEscala") is { } primeiro ? primeiro : inicio;
        var semana = Duracao("jornadaSemana");
        var sabado = EscalaPorCiclo ? 0 : Duracao("jornadaSabado");
        var escala = Campo("escala").Indice;

        var grade = Grade("dias");
        var anteriores = grade.Linhas.ToHashSet();
        var datas = new List<string>();
        for (var data = inicio; data <= fim; data = data.AddDays(1))
            datas.Add(data.ToString("dd/MM/yyyy", Cultura));
        grade.Gerar(datas, "data");
        var aviso = grade.Aviso;
        foreach (var linha in grade.Linhas.Where(linha => !anteriores.Contains(linha)))
        {
            var data = LeituraCampoMobile.Data(linha.Campo("data"));
            var (tipo, minutos) = escala switch
            {
                // Seis dias de trabalho e o sétimo de folga, contados do primeiro dia de trabalho do ciclo.
                SeisPorUm => Posicao(data, inicioCiclo, 7) < 6 ? (TipoDia.Util, semana) : (TipoDia.Descanso, 0),
                // Um dia de trabalho e o seguinte de descanso: as 36 horas ininterruptas (CLT, art. 59-A).
                DozePorTrintaESeis => Posicao(data, inicioCiclo, 2) == 0 ? (TipoDia.Util, semana) : (TipoDia.Descanso, 0),
                _ => data.DayOfWeek switch
                {
                    DayOfWeek.Sunday => (TipoDia.Descanso, 0),
                    DayOfWeek.Saturday => (TipoDia.Util, sabado),
                    _ => (TipoDia.Util, semana)
                }
            };
            // Os dias úteis com jornada já vêm com o horário padrão; no sábado da escala semanal, só o primeiro período.
            var (e1, s1, e2, s2) = minutos == 0 ? ("", "", "", "")
                : escala == Semanal && data.DayOfWeek == DayOfWeek.Saturday ? (Texto("entrada1"), Texto("saida1"), "", "")
                : (Texto("entrada1"), Texto("saida1"), Texto("entrada2"), Texto("saida2"));
            linha.Campo("tipo").Indice = (int)tipo;
            linha.Campo("previsto").Valor = HorasDeRelogio(minutos);
            linha.Campo("entrada1").Valor = e1;
            linha.Campo("saida1").Valor = s1;
            linha.Campo("entrada2").Valor = e2;
            linha.Campo("saida2").Valor = s2;
        }
        // Preencher as linhas novas limpa o aviso da grade; ele explica o que foi criado e preservado.
        grade.Aviso = aviso;
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        _ultimaApuracao = null;
        var grade = Grade("dias");
        // Como o Apurar do desktop: sem dias, gera o período antes de apurar.
        if (grade.Linhas.Count == 0)
            GerarDiasDoPeriodo();
        if (grade.Linhas.Count == 0)
            throw new EntradaInvalidaException(grade.Aviso ?? "Gere os dias do período e informe as marcações antes de calcular.");
        var mes = Competencia("competencia");
        var marcacoes = grade.Ler(LerDia);
        var apuracao = _apuracao.Apurar(new ApurarJornadaRequest(mes, (TrabalhoNoturno)Opcao("noturno"), marcacoes, Opcao("escala") == DozePorTrintaESeis));
        if (apuracao.Falhou)
            return Task.FromResult<Result<DemonstrativoDto>>(apuracao.Erro);
        _ultimaApuracao = apuracao.Valor;
        return Task.FromResult<Result<DemonstrativoDto>>(DemonstrativoJornada.Montar(apuracao.Valor, marcacoes));
    }

    private async Task TransferirJornadaAsync(TipoCalculadoraMobile destino)
    {
        if (_transferencias is null || Demonstrativo is null || _ultimaApuracao is not { } apuracao) return;
        var totais = apuracao.Totais;
        static ValorCampoMobile Valor(string valor) => new(valor, 0, false);
        static string Horas(int minutos) => $"{minutos / 60}:{minutos % 60:00}";
        var campos = new Dictionary<string, ValorCampoMobile>
        {
            ["competencia"] = Valor(apuracao.Competencia.ToString("MM/yyyy", Cultura)),
            ["horas1"] = Valor(Horas(totais.ExtrasFaixa1)), ["horas2"] = Valor(Horas(totais.ExtrasFaixa2)),
            ["trabalho"] = new("", apuracao.Noturno == TrabalhoNoturno.Urbano ? 0 : 1, false),
            ["extrasNoturnas"] = Valor(Horas(totais.ExtrasNoturnas)),
            ["feriados"] = Valor(totais.Feriados.ToString(Cultura))
        };
        campos[destino == TipoCalculadoraMobile.Holerite ? "horasNoturnas" : "noturnas"] = Valor(Horas(totais.Noturnas));
        if (destino == TipoCalculadoraMobile.Holerite)
        {
            campos["faltas"] = Valor(totais.Faltas.ToString(Cultura));
            campos["descansos"] = Valor(totais.DescansosPerdidos.ToString(Cultura));
            campos["atrasos"] = Valor(Horas(totais.Faltantes));
            campos["intervaloIntra"] = Valor(Horas(totais.IntervaloSuprimido));
            campos["intervaloInter"] = Valor(Horas(totais.InterjornadaSuprimida));
        }
        await _transferencias.AbrirComDadosAsync(destino, new DadosFormularioMobile(1, campos,
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, ValorCampoMobile>>>()));
    }

    private static MarcacaoDia LerDia(LinhaGradeMobile linha)
    {
        var data = LeituraCampoMobile.Data(linha.Campo("data"));
        var dia = data.ToString("dd/MM ddd", Cultura);
        var tipo = (TipoDia)LeituraCampoMobile.Opcao(linha.Campo("tipo"));
        if (!LeituraDeHoras.TentarLerDuracao(linha.Campo("previsto").Valor, out var previsto))
            throw new EntradaInvalidaException($"informe a jornada prevista de {dia} em horas, como 8:00.");
        var periodos = new List<PeriodoTrabalhado>(2);
        foreach (var (entrada, saida) in new[] { (linha.Campo("entrada1").Valor, linha.Campo("saida1").Valor), (linha.Campo("entrada2").Valor, linha.Campo("saida2").Valor) })
        {
            var semEntrada = string.IsNullOrWhiteSpace(entrada);
            var semSaida = string.IsNullOrWhiteSpace(saida);
            if (semEntrada && semSaida)
                continue;
            if (semEntrada || semSaida || !LeituraDeHoras.TentarLerHorario(entrada, out var horaEntrada) || !LeituraDeHoras.TentarLerHorario(saida, out var horaSaida))
                throw new EntradaInvalidaException($"em {dia}, informe a entrada e a saída como horários, por exemplo 08:00 e 12:00.");
            periodos.Add(new PeriodoTrabalhado(horaEntrada, horaSaida));
        }
        return new MarcacaoDia(data, tipo, previsto, periodos);
    }

    private (DateOnly Inicio, DateOnly Fim) LerPeriodo()
    {
        var inicio = Data("inicio");
        var fim = Data("fim");
        var dias = fim.DayNumber - inicio.DayNumber + 1;
        if (dias is < 1 or > ApurarJornadaRequest.MaximoDias)
            throw new EntradaInvalidaException($"O fim do período do ponto deve ser igual ou posterior ao início, com até {ApurarJornadaRequest.MaximoDias} dias.");
        return (inicio, fim);
    }

    /// <summary>Duração em minutos pela mesma leitura do desktop (8:00, 8h, 8h30, 0800 ou 8); vazio vale zero.</summary>
    private int Duracao(string chave)
    {
        var campo = Campo(chave);
        if (!LeituraDeHoras.TentarLerDuracao(campo.Valor, out var minutos))
            throw new EntradaInvalidaException($"{campo.Rotulo}: informe a jornada em horas, como 8:00.");
        return minutos;
    }

    /// <summary>Posição do dia no ciclo da escala, inclusive antes do primeiro dia de trabalho informado.</summary>
    private static int Posicao(DateOnly data, DateOnly inicioCiclo, int tamanho) => ((data.DayNumber - inicioCiclo.DayNumber) % tamanho + tamanho) % tamanho;

    private static DateOnly? LerCompetencia(string texto) =>
        DateOnly.TryParseExact("01/" + texto.Trim(), "dd/MM/yyyy", Cultura, System.Globalization.DateTimeStyles.None, out var data) ? data : null;

    private static (string Inicio, string Fim) MesInteiro(DateOnly mes) =>
        (mes.ToString("dd/MM/yyyy", Cultura), mes.AddMonths(1).AddDays(-1).ToString("dd/MM/yyyy", Cultura));

    private static string HorasDeRelogio(int minutos) => $"{minutos / 60}:{minutos % 60:00}";
}
