using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Judicial;
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
/// Atualização de débitos judiciais trabalhistas e cíveis: as parcelas com vencimento e valor, as datas do processo e os
/// critérios da decisão; o cálculo segue as fases dos tribunais superiores e da Lei 14.905/2024.
/// </summary>
public sealed class DebitoJudicialViewModel : ViewModelBase, ICalculoSalvavel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly ISimularDebitoJudicialUseCase _simulador;
    private readonly IUserNotifier _notificador;
    private readonly IRelatorioPdfService _relatorioPdf;
    private readonly IPlanilhaService _planilha;
    private readonly IArquivoDialogService _arquivoDialog;
    private OpcaoCampo _naturezaSelecionada;
    private string _dataCalculo;
    private string _dataAjuizamento = string.Empty;
    private OpcaoCampo _inicioJurosSelecionado;
    private string _dataCitacao = string.Empty;
    private OpcaoCampo _indiceSelecionado;
    private OpcaoCampo _acrescimosSelecionados;
    private SimulacaoDebitoJudicialDto? _ultimaSimulacao;
    private bool _temResultado;
    private IReadOnlyList<IndicadorResumoViewModel> _indicadores = [];
    private IReadOnlyList<string> _criterios = [];
    private IReadOnlyList<string> _observacoes = [];

    public DebitoJudicialViewModel(ISimularDebitoJudicialUseCase simulador, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha,
        IArquivoDialogService arquivoDialog, IHistoricoDaJanelaFactory historico)
    {
        _simulador = simulador;
        _notificador = notificador;
        _relatorioPdf = relatorioPdf;
        _planilha = planilha;
        _arquivoDialog = arquivoDialog;
        _naturezaSelecionada = OpcoesNatureza[0];
        _inicioJurosSelecionado = OpcoesInicioJuros[0];
        _indiceSelecionado = OpcoesIndice[0];
        _acrescimosSelecionados = OpcoesAcrescimos[0];
        _dataCalculo = DateTime.Today.ToString("dd/MM/yyyy", Cultura);
        Historico = historico.Criar(this);
        Parcelas.Add(NovaParcela());
        Parcelas.CollectionChanged += (_, _) => Desatualizar();
        AdicionarParcelaCommand = new RelayCommand(_ => Parcelas.Add(NovaParcela()));
        RemoverParcelaCommand = new RelayCommand(parametro =>
        {
            if (parametro is ParcelaDebitoViewModel parcela && Parcelas.Count > 1)
                Parcelas.Remove(parcela);
        });
        CalcularCommand = new AsyncRelayCommand(CalcularAsync);
        ExportarPdfCommand = new AsyncRelayCommand(ExportarPdfAsync, () => _ultimaSimulacao is not null);
        ExportarExcelCommand = new AsyncRelayCommand(
            () => ExportacaoPlanilha.SalvarAsync(_arquivoDialog, _notificador, $"debito-judicial-{_ultimaSimulacao!.DataCalculo:yyyy-MM-dd}.xlsx",
                caminho => _planilha.GerarDebitoJudicialAsync(_ultimaSimulacao!, caminho, CancellationToken.None)),
            () => _ultimaSimulacao is not null);
    }

    public IReadOnlyList<OpcaoCampo> OpcoesNatureza { get; } = [new("Trabalhista", NaturezaDebito.Trabalhista), new("Cível", NaturezaDebito.Civel)];
    public OpcaoCampo NaturezaSelecionada
    {
        get => _naturezaSelecionada;
        set
        {
            if (!SetProperty(ref _naturezaSelecionada, value))
                return;
            OnPropertyChanged(nameof(EhTrabalhista));
            OnPropertyChanged(nameof(EhCivel));
            OnPropertyChanged(nameof(UsaCitacao));
            Desatualizar();
        }
    }
    public bool EhTrabalhista => (NaturezaDebito)NaturezaSelecionada.Valor == NaturezaDebito.Trabalhista;
    public bool EhCivel => !EhTrabalhista;

    public string DataCalculo { get => _dataCalculo; set { if (SetProperty(ref _dataCalculo, value)) Desatualizar(); } }
    public string DataAjuizamento { get => _dataAjuizamento; set { if (SetProperty(ref _dataAjuizamento, value)) Desatualizar(); } }

    public IReadOnlyList<OpcaoCampo> OpcoesInicioJuros { get; } = [new("Do vencimento", InicioJurosCivel.Vencimento), new("Da citação", InicioJurosCivel.Citacao)];
    public OpcaoCampo InicioJurosSelecionado
    {
        get => _inicioJurosSelecionado;
        set
        {
            if (!SetProperty(ref _inicioJurosSelecionado, value))
                return;
            OnPropertyChanged(nameof(UsaCitacao));
            Desatualizar();
        }
    }
    public bool UsaCitacao => EhCivel && (InicioJurosCivel)InicioJurosSelecionado.Valor == InicioJurosCivel.Citacao;
    public string DataCitacao { get => _dataCitacao; set { if (SetProperty(ref _dataCitacao, value)) Desatualizar(); } }

    public IReadOnlyList<OpcaoCampo> OpcoesIndice { get; } = [new("INPC", IndiceCivelAnterior.Inpc), new("IPCA", IndiceCivelAnterior.Ipca)];
    public OpcaoCampo IndiceSelecionado { get => _indiceSelecionado; set { if (SetProperty(ref _indiceSelecionado, value)) Desatualizar(); } }

    public IReadOnlyList<OpcaoCampo> OpcoesAcrescimos { get; } =
    [
        new("Não incluir", AcrescimosPenhora.Nenhum),
        new("Multa de 10% e honorários de 10%", AcrescimosPenhora.MultaEHonorarios),
        new("Só a multa de 10%", AcrescimosPenhora.Multa)
    ];
    public OpcaoCampo AcrescimosSelecionados { get => _acrescimosSelecionados; set { if (SetProperty(ref _acrescimosSelecionados, value)) Desatualizar(); } }

    public ObservableCollection<ParcelaDebitoViewModel> Parcelas { get; } = [];
    public bool TemResultado { get => _temResultado; private set => SetProperty(ref _temResultado, value); }
    public IReadOnlyList<IndicadorResumoViewModel> Indicadores { get => _indicadores; private set => SetProperty(ref _indicadores, value); }
    public IReadOnlyList<string> Criterios { get => _criterios; private set => SetProperty(ref _criterios, value); }
    public IReadOnlyList<string> Observacoes { get => _observacoes; private set { SetProperty(ref _observacoes, value); OnPropertyChanged(nameof(TemObservacoes)); } }
    public bool TemObservacoes => Observacoes.Count > 0;
    public ICommand AdicionarParcelaCommand { get; }
    public ICommand RemoverParcelaCommand { get; }
    public ICommand CalcularCommand { get; }
    public ICommand ExportarPdfCommand { get; }
    public ICommand ExportarExcelCommand { get; }
    public HistoricoDaJanela Historico { get; }

    public string TipoHistorico => "DebitoJudicial";
    public string NomeCalculadora => "Débitos judiciais";
    public string NomeSugerido => $"Débito {NaturezaSelecionada.Texto.ToLower(Cultura)} - cálculo em {DataCalculo}";

    public DadosFormulario ExportarDados() => new(
        new()
        {
            [nameof(NaturezaSelecionada)] = NaturezaSelecionada.Texto,
            [nameof(DataCalculo)] = DataCalculo,
            [nameof(DataAjuizamento)] = DataAjuizamento,
            [nameof(InicioJurosSelecionado)] = InicioJurosSelecionado.Texto,
            [nameof(DataCitacao)] = DataCitacao,
            [nameof(IndiceSelecionado)] = IndiceSelecionado.Texto,
            [nameof(AcrescimosSelecionados)] = AcrescimosSelecionados.Texto
        },
        Parcelas.Select(parcela => new Dictionary<string, string>
        {
            [nameof(ParcelaDebitoViewModel.Descricao)] = parcela.Descricao,
            [nameof(ParcelaDebitoViewModel.Vencimento)] = parcela.Vencimento,
            [nameof(ParcelaDebitoViewModel.Valor)] = parcela.Valor
        }).ToList());

    public void ImportarDados(DadosFormulario dados)
    {
        NaturezaSelecionada = Opcao(OpcoesNatureza, dados.Valor(nameof(NaturezaSelecionada))) ?? NaturezaSelecionada;
        DataCalculo = dados.Valor(nameof(DataCalculo), DataCalculo);
        DataAjuizamento = dados.Valor(nameof(DataAjuizamento), DataAjuizamento);
        InicioJurosSelecionado = Opcao(OpcoesInicioJuros, dados.Valor(nameof(InicioJurosSelecionado))) ?? InicioJurosSelecionado;
        DataCitacao = dados.Valor(nameof(DataCitacao), DataCitacao);
        IndiceSelecionado = Opcao(OpcoesIndice, dados.Valor(nameof(IndiceSelecionado))) ?? IndiceSelecionado;
        AcrescimosSelecionados = Opcao(OpcoesAcrescimos, dados.Valor(nameof(AcrescimosSelecionados))) ?? AcrescimosSelecionados;
        if (dados.Linhas is not { Count: > 0 } linhas)
            return;
        Parcelas.Clear();
        foreach (var linha in linhas)
            Parcelas.Add(new ParcelaDebitoViewModel(linha.GetValueOrDefault(nameof(ParcelaDebitoViewModel.Descricao), ""),
                linha.GetValueOrDefault(nameof(ParcelaDebitoViewModel.Vencimento), ""), linha.GetValueOrDefault(nameof(ParcelaDebitoViewModel.Valor), "0,00"), Desatualizar));
    }

    public Task RecalcularAsync() => CalcularAsync();

    private ParcelaDebitoViewModel NovaParcela() => new($"Parcela {Parcelas.Count + 1}", "", "0,00", Desatualizar);

    private async Task CalcularAsync()
    {
        if (!TentarLerData(DataCalculo, "a data do cálculo", obrigatoria: true, out var calculo)
            || !TentarLerData(DataAjuizamento, "a data do ajuizamento", obrigatoria: false, out var ajuizamento)
            || !TentarLerData(DataCitacao, "a data da citação", obrigatoria: UsaCitacao, out var citacao))
            return;
        var parcelas = new List<ParcelaDebito>(Parcelas.Count);
        for (var i = 0; i < Parcelas.Count; i++)
        {
            if (!Parcelas[i].TentarLer(i + 1, out var parcela, out var erro))
            {
                _notificador.MostrarAviso(erro);
                return;
            }
            parcelas.Add(parcela);
        }

        try
        {
            var simulacao = await _simulador.CalcularAsync(new SimularDebitoJudicialRequest((NaturezaDebito)NaturezaSelecionada.Valor, parcelas, calculo!.Value,
                EhTrabalhista ? ajuizamento : null, (InicioJurosCivel)InicioJurosSelecionado.Valor, UsaCitacao ? citacao : null, (IndiceCivelAnterior)IndiceSelecionado.Valor,
                EhCivel ? (AcrescimosPenhora)AcrescimosSelecionados.Valor : AcrescimosPenhora.Nenhum), CancellationToken.None);
            if (simulacao.Falhou)
                _notificador.MostrarFalha(simulacao.Erro, "Não foi possível atualizar o débito.");
            else
                Apresentar(simulacao.Valor);
        }
        catch (Exception ex) { _notificador.MostrarErro("Não foi possível atualizar o débito.", ex); }
    }

    private void Apresentar(SimulacaoDebitoJudicialDto simulacao)
    {
        _ultimaSimulacao = simulacao;
        for (var i = 0; i < Parcelas.Count; i++)
            Parcelas[i].Apresentar(null);
        // As parcelas do resultado vêm por vencimento; a tela mantém a ordem digitada.
        var restantes = simulacao.Parcelas.ToList();
        foreach (var linha in Parcelas)
        {
            if (!linha.TentarLer(0, out var parcela, out _))
                continue;
            var resultado = restantes.FirstOrDefault(item => item.Vencimento == parcela.Vencimento && item.Valor == parcela.Valor);
            if (resultado is null)
                continue;
            restantes.Remove(resultado);
            linha.Apresentar(resultado);
        }
        Indicadores =
        [
            new("Total atualizado", Moeda(simulacao.TotalComAcrescimos), simulacao.TemAcrescimos ? "Com multa e honorários" : $"Em {simulacao.DataCalculo:dd/MM/yyyy}"),
            new("Valor original", Moeda(simulacao.TotalValor), simulacao.Parcelas.Count == 1 ? "1 parcela" : $"{simulacao.Parcelas.Count} parcelas"),
            new("Correção", Moeda(simulacao.TotalCorrecao), simulacao.Natureza == NaturezaDebito.Trabalhista ? "IPCA-E, Selic e IPCA" : "Índice, Selic e IPCA"),
            new("Juros", Moeda(simulacao.TotalJuros), simulacao.Natureza == NaturezaDebito.Trabalhista ? "TR e taxa legal" : "Taxa legal"),
            new("Multa e honorários", Moeda(simulacao.Multa + simulacao.Honorarios), simulacao.TemAcrescimos ? "CPC, art. 523, § 1º" : "Não incluídos")
        ];
        Criterios = simulacao.Criterios;
        Observacoes = simulacao.Observacoes;
        TemResultado = true;
        AtualizarComandos();
    }

    private void Desatualizar()
    {
        if (_ultimaSimulacao is null && !TemResultado)
            return;
        _ultimaSimulacao = null;
        foreach (var parcela in Parcelas)
            parcela.Apresentar(null);
        TemResultado = false;
        AtualizarComandos();
    }

    private void AtualizarComandos()
    {
        ((AsyncRelayCommand)ExportarPdfCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportarExcelCommand).RaiseCanExecuteChanged();
    }

    private async Task ExportarPdfAsync()
    {
        if (_ultimaSimulacao is null)
            return;
        var caminho = _arquivoDialog.SolicitarDestinoPdf($"debito-judicial-{_ultimaSimulacao.DataCalculo:yyyy-MM-dd}.pdf");
        if (caminho is null)
            return;
        try
        {
            await _relatorioPdf.GerarRelatorioDebitoJudicialAsync(_ultimaSimulacao, caminho, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro("Não foi possível gerar o relatório em PDF.", exception);
        }
    }

    private bool TentarLerData(string texto, string campo, bool obrigatoria, out DateOnly? data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(texto) && !obrigatoria)
            return true;
        if (DateOnly.TryParseExact(texto.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var lida))
        {
            data = lida;
            return true;
        }
        _notificador.MostrarAviso($"Informe {campo} no formato dd/mm/aaaa.");
        return false;
    }

    private static OpcaoCampo? Opcao(IReadOnlyList<OpcaoCampo> opcoes, string texto) => opcoes.FirstOrDefault(opcao => opcao.Texto == texto);
    private static string Moeda(decimal valor) => valor.ToString("C2", Cultura);
}
