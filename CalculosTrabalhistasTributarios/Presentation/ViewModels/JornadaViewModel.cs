using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.Services;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>
/// Jornada pelas marcações de ponto: gera os dias do mês com o horário padrão, recebe as marcações de cada dia e apura
/// horas extras, noturnas, faltas, atrasos e intervalos, que podem ir para a calculadora de horas extras ou para o holerite.
/// </summary>
public sealed class JornadaViewModel : ViewModelBase, ICalculoSalvavel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly IApurarJornadaUseCase _apuracao;
    private readonly IUserNotifier _notificador;
    private readonly IPlanilhaService _planilha;
    private readonly IArquivoDialogService _arquivoDialog;
    private readonly IWindowNavigator _navegador;
    private string _competencia;
    private string _jornadaSemana = "8:00";
    private string _jornadaSabado = "4:00";
    private string _entrada1 = "08:00";
    private string _saida1 = "12:00";
    private string _entrada2 = "13:00";
    private string _saida2 = "17:00";
    private OpcaoCampo _noturnoSelecionado;
    private SimulacaoJornadaDto? _ultimaApuracao;
    private bool _temResultado;
    private IReadOnlyList<IndicadorResumoViewModel> _indicadores = [];
    private IReadOnlyList<string> _criterios = [];
    private IReadOnlyList<string> _observacoes = [];

    public JornadaViewModel(IApurarJornadaUseCase apuracao, IUserNotifier notificador, IPlanilhaService planilha, IArquivoDialogService arquivoDialog,
        IWindowNavigator navegador, ContextoCompartilhado contexto, IHistoricoDaJanelaFactory historico)
    {
        _apuracao = apuracao;
        _notificador = notificador;
        _planilha = planilha;
        _arquivoDialog = arquivoDialog;
        _navegador = navegador;
        _noturnoSelecionado = OpcoesNoturno[0];
        _competencia = (contexto.Atual.Competencia ?? DateOnly.FromDateTime(DateTime.Today)).ToString("MM/yyyy", Cultura);
        Historico = historico.Criar(this);
        Dias.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemDias));
        GerarDiasCommand = new RelayCommand(_ => GerarDias());
        ApurarCommand = new RelayCommand(_ => Apurar());
        UsarNasHorasExtrasCommand = new RelayCommand(_ => Usar(TipoCalculadora.HorasExtras), _ => _ultimaApuracao is not null);
        UsarNoHoleriteCommand = new RelayCommand(_ => Usar(TipoCalculadora.Holerite), _ => _ultimaApuracao is not null);
        ExportarExcelCommand = new AsyncRelayCommand(
            () => ExportacaoPlanilha.SalvarAsync(_arquivoDialog, _notificador, $"jornada-{_ultimaApuracao!.Competencia:MM-yyyy}.xlsx",
                caminho => _planilha.GerarJornadaAsync(_ultimaApuracao!, caminho, CancellationToken.None)),
            () => _ultimaApuracao is not null);
    }

    public string Competencia { get => _competencia; set { if (SetProperty(ref _competencia, value)) Desatualizar(); } }
    public string JornadaSemana { get => _jornadaSemana; set => SetProperty(ref _jornadaSemana, value); }
    public string JornadaSabado { get => _jornadaSabado; set => SetProperty(ref _jornadaSabado, value); }
    public string Entrada1 { get => _entrada1; set => SetProperty(ref _entrada1, value); }
    public string Saida1 { get => _saida1; set => SetProperty(ref _saida1, value); }
    public string Entrada2 { get => _entrada2; set => SetProperty(ref _entrada2, value); }
    public string Saida2 { get => _saida2; set => SetProperty(ref _saida2, value); }

    public IReadOnlyList<OpcaoCampo> OpcoesNoturno { get; } =
    [
        new("Urbano (22h às 5h)", TrabalhoNoturno.Urbano),
        new("Rural, lavoura (21h às 5h)", TrabalhoNoturno.RuralLavoura),
        new("Rural, pecuária (20h às 4h)", TrabalhoNoturno.RuralPecuaria)
    ];
    public OpcaoCampo NoturnoSelecionado { get => _noturnoSelecionado; set { if (SetProperty(ref _noturnoSelecionado, value)) Desatualizar(); } }

    public ObservableCollection<DiaJornadaViewModel> Dias { get; } = [];
    public bool TemDias => Dias.Count > 0;
    public bool TemResultado { get => _temResultado; private set => SetProperty(ref _temResultado, value); }
    public IReadOnlyList<IndicadorResumoViewModel> Indicadores { get => _indicadores; private set => SetProperty(ref _indicadores, value); }
    public IReadOnlyList<string> Criterios { get => _criterios; private set => SetProperty(ref _criterios, value); }
    public IReadOnlyList<string> Observacoes { get => _observacoes; private set { SetProperty(ref _observacoes, value); OnPropertyChanged(nameof(TemObservacoes)); } }
    public bool TemObservacoes => Observacoes.Count > 0;
    public ICommand GerarDiasCommand { get; }
    public ICommand ApurarCommand { get; }
    public ICommand UsarNasHorasExtrasCommand { get; }
    public ICommand UsarNoHoleriteCommand { get; }
    public ICommand ExportarExcelCommand { get; }
    public HistoricoDaJanela Historico { get; }

    public string TipoHistorico => "Jornada";
    public string NomeCalculadora => "Jornada pelo ponto";
    public string NomeSugerido => $"Jornada - {Competencia}";

    public DadosFormulario ExportarDados() => new(
        new()
        {
            [nameof(Competencia)] = Competencia,
            [nameof(JornadaSemana)] = JornadaSemana,
            [nameof(JornadaSabado)] = JornadaSabado,
            [nameof(Entrada1)] = Entrada1,
            [nameof(Saida1)] = Saida1,
            [nameof(Entrada2)] = Entrada2,
            [nameof(Saida2)] = Saida2,
            [nameof(NoturnoSelecionado)] = NoturnoSelecionado.Texto
        },
        Dias.Select(dia => new Dictionary<string, string>
        {
            ["Data"] = dia.Data.ToString("dd/MM/yyyy", Cultura),
            ["Tipo"] = dia.TipoSelecionado.Texto,
            [nameof(DiaJornadaViewModel.Previsto)] = dia.Previsto,
            [nameof(DiaJornadaViewModel.Entrada1)] = dia.Entrada1,
            [nameof(DiaJornadaViewModel.Saida1)] = dia.Saida1,
            [nameof(DiaJornadaViewModel.Entrada2)] = dia.Entrada2,
            [nameof(DiaJornadaViewModel.Saida2)] = dia.Saida2
        }).ToList());

    public void ImportarDados(DadosFormulario dados)
    {
        Competencia = dados.Valor(nameof(Competencia), Competencia);
        JornadaSemana = dados.Valor(nameof(JornadaSemana), JornadaSemana);
        JornadaSabado = dados.Valor(nameof(JornadaSabado), JornadaSabado);
        Entrada1 = dados.Valor(nameof(Entrada1), Entrada1);
        Saida1 = dados.Valor(nameof(Saida1), Saida1);
        Entrada2 = dados.Valor(nameof(Entrada2), Entrada2);
        Saida2 = dados.Valor(nameof(Saida2), Saida2);
        NoturnoSelecionado = OpcoesNoturno.FirstOrDefault(opcao => opcao.Texto == dados.Valor(nameof(NoturnoSelecionado))) ?? NoturnoSelecionado;
        Dias.Clear();
        foreach (var linha in dados.Linhas ?? [])
        {
            if (!DateOnly.TryParseExact(linha.GetValueOrDefault("Data"), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data))
                continue;
            var tipo = DiaJornadaViewModel.Tipos.FirstOrDefault(opcao => opcao.Texto == linha.GetValueOrDefault("Tipo"))?.Valor as TipoDia? ?? TipoDia.Util;
            Dias.Add(new DiaJornadaViewModel(data, tipo, linha.GetValueOrDefault(nameof(DiaJornadaViewModel.Previsto), ""),
                linha.GetValueOrDefault(nameof(DiaJornadaViewModel.Entrada1), ""), linha.GetValueOrDefault(nameof(DiaJornadaViewModel.Saida1), ""),
                linha.GetValueOrDefault(nameof(DiaJornadaViewModel.Entrada2), ""), linha.GetValueOrDefault(nameof(DiaJornadaViewModel.Saida2), ""), Desatualizar));
        }
    }

    public Task RecalcularAsync()
    {
        if (TemDias)
            Apurar();
        return Task.CompletedTask;
    }

    private void GerarDias()
    {
        if (!TentarLerCompetencia(out var mes))
            return;
        if (!LeituraDeHoras.TentarLerDuracao(JornadaSemana, out var semana) || !LeituraDeHoras.TentarLerDuracao(JornadaSabado, out var sabado))
        {
            _notificador.MostrarAviso("Informe as jornadas de segunda a sexta e de sábado em horas, como 8:00 e 4:00.");
            return;
        }
        if (Dias.Any(dia => dia.Entrada1.Length + dia.Entrada2.Length > 0)
            && !_notificador.Confirmar("Gerar os dias de novo substitui as marcações informadas. Continuar?", "Gerar dias"))
            return;

        Dias.Clear();
        for (var data = mes; data.Month == mes.Month; data = data.AddDays(1))
        {
            var (tipo, minutos) = data.DayOfWeek switch
            {
                DayOfWeek.Sunday => (TipoDia.Descanso, 0),
                DayOfWeek.Saturday => (TipoDia.Util, sabado),
                _ => (TipoDia.Util, semana)
            };
            // Os dias úteis com jornada já vêm com o horário padrão; no sábado, só o primeiro período.
            var (e1, s1, e2, s2) = minutos == 0 ? ("", "", "", "")
                : data.DayOfWeek == DayOfWeek.Saturday ? (Entrada1, Saida1, "", "")
                : (Entrada1, Saida1, Entrada2, Saida2);
            Dias.Add(new DiaJornadaViewModel(data, tipo, DiaJornadaViewModel.Horas(minutos), e1, s1, e2, s2, Desatualizar));
        }
        Desatualizar();
    }

    private void Apurar()
    {
        if (!TemDias)
            GerarDias();
        if (!TemDias || !TentarLerCompetencia(out var mes))
            return;
        var marcacoes = new List<MarcacaoDia>(Dias.Count);
        foreach (var dia in Dias)
        {
            if (!dia.TentarLer(out var marcacao, out var erro))
            {
                _notificador.MostrarAviso(erro);
                return;
            }
            marcacoes.Add(marcacao);
        }

        try
        {
            var apuracao = _apuracao.Apurar(new ApurarJornadaRequest(mes, (TrabalhoNoturno)NoturnoSelecionado.Valor, marcacoes));
            if (apuracao.Falhou)
                _notificador.MostrarFalha(apuracao.Erro, "Não foi possível apurar a jornada.");
            else
                Apresentar(apuracao.Valor);
        }
        catch (Exception ex) { _notificador.MostrarErro("Não foi possível apurar a jornada.", ex); }
    }

    private void Apresentar(SimulacaoJornadaDto apuracao)
    {
        _ultimaApuracao = apuracao;
        var porDia = apuracao.Dias.ToDictionary(dia => dia.Data);
        foreach (var dia in Dias)
            dia.Apresentar(porDia.GetValueOrDefault(dia.Data));
        var t = apuracao.Totais;
        Indicadores =
        [
            new("Horas trabalhadas", Horas(t.Trabalhadas), $"Previstas: {Horas(t.Previstas)}"),
            new("Extras dos dias úteis", Horas(t.ExtrasFaixa1 + t.ExtrasNoturnas), t.ExtrasNoturnas > 0 ? $"{Horas(t.ExtrasNoturnas)} no período noturno" : "Adicional da faixa 1"),
            new("Extras em descanso e feriado", Horas(t.ExtrasFaixa2), "Adicional da faixa 2"),
            new("Horas noturnas", Horas(t.Noturnas + t.ExtrasNoturnas), "Horas de relógio"),
            new("Faltas e atrasos", t.Faltas == 1 ? "1 dia" : $"{t.Faltas} dias", $"{Horas(t.Faltantes)} de atrasos; {t.DescansosPerdidos} DSR perdido(s)"),
            new("Intervalos suprimidos", Horas(t.IntervaloSuprimido + t.InterjornadaSuprimida), $"Intrajornada {Horas(t.IntervaloSuprimido)} • entre jornadas {Horas(t.InterjornadaSuprimida)}")
        ];
        Criterios = apuracao.Criterios;
        Observacoes = apuracao.Observacoes;
        TemResultado = true;
        AtualizarComandos();
    }

    /// <summary>Abre a calculadora de horas extras ou o holerite com as horas apuradas; o salário e os demais dados ficam com o usuário.</summary>
    private void Usar(TipoCalculadora tipo)
    {
        if (_ultimaApuracao is not { } apuracao)
            return;
        var t = apuracao.Totais;
        var valores = new Dictionary<string, string>
        {
            ["Competência"] = apuracao.Competencia.ToString("MM/yyyy", Cultura),
            ["Horas extras (faixa 1)"] = Horas(t.ExtrasFaixa1),
            ["Horas extras (faixa 2)"] = Horas(t.ExtrasFaixa2),
            ["Trabalho noturno"] = apuracao.Noturno == TrabalhoNoturno.Urbano ? "Urbano (22h às 5h)" : "Rural",
            ["Horas noturnas (relógio)"] = Horas(t.Noturnas),
            ["Horas extras noturnas (relógio)"] = Horas(t.ExtrasNoturnas),
            ["Feriados no mês"] = t.Feriados.ToString(Cultura)
        };
        if (tipo == TipoCalculadora.Holerite)
        {
            valores["Faltas (dias)"] = t.Faltas.ToString(Cultura);
            valores["Descansos perdidos"] = t.DescansosPerdidos.ToString(Cultura);
            valores["Atrasos (horas)"] = Horas(t.Faltantes);
        }
        _navegador.AbrirCalculadora(tipo, valores);
    }

    private void Desatualizar()
    {
        if (_ultimaApuracao is null && !TemResultado)
            return;
        _ultimaApuracao = null;
        foreach (var dia in Dias)
            dia.Apresentar(null);
        TemResultado = false;
        AtualizarComandos();
    }

    private void AtualizarComandos()
    {
        ((RelayCommand)UsarNasHorasExtrasCommand).RaiseCanExecuteChanged();
        ((RelayCommand)UsarNoHoleriteCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportarExcelCommand).RaiseCanExecuteChanged();
    }

    private bool TentarLerCompetencia(out DateOnly mes)
    {
        mes = default;
        if (DateTime.TryParseExact(Competencia.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data))
        {
            mes = DateOnly.FromDateTime(data);
            return true;
        }
        _notificador.MostrarAviso("Informe a competência no formato mm/aaaa.");
        return false;
    }

    private static string Horas(int minutos) => DiaJornadaViewModel.Horas(minutos);
}
