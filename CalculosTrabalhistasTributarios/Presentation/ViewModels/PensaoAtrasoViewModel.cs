using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Pensao;
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
/// Pensão alimentícia em atraso: gera as parcelas do período, recebe os pagamentos parciais e calcula a correção, os juros e
/// a separação entre o rito da prisão e o da penhora.
/// </summary>
public sealed class PensaoAtrasoViewModel : ViewModelBase, ICalculoSalvavel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly ISimularPensaoAtrasoUseCase _simulador;
    private readonly IUserNotifier _notificador;
    private readonly IRelatorioPdfService _relatorioPdf;
    private readonly IPlanilhaService _planilha;
    private readonly IArquivoDialogService _arquivoDialog;
    private OpcaoCampo _baseSelecionada;
    private string _valorPensao = "0,00";
    private string _percentual = "0,00";
    private string _diaVencimento = "10";
    private string _primeiraParcela;
    private string _ultimaParcela;
    private string _dataCalculo;
    private string _dataAjuizamento = string.Empty;
    private OpcaoCampo _correcaoSelecionada;
    private OpcaoCampo _jurosSelecionado;
    private OpcaoCampo _acrescimosSelecionados;
    private SimulacaoPensaoAtrasoDto? _ultimaSimulacao;
    // As parcelas da lista foram geradas com outro valor, percentual, dia ou período e precisam ser geradas de novo.
    private bool _parcelasDesatualizadas;
    private bool _temResultado;
    private IReadOnlyList<IndicadorResumoViewModel> _indicadoresResumo = [];
    private IReadOnlyList<string> _criterios = [];
    private IReadOnlyList<string> _observacoes = [];

    public PensaoAtrasoViewModel(ISimularPensaoAtrasoUseCase simulador, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha, IArquivoDialogService arquivoDialog,
        IHistoricoDaJanelaFactory historico)
    {
        _simulador = simulador; _notificador = notificador; _relatorioPdf = relatorioPdf; _planilha = planilha; _arquivoDialog = arquivoDialog;
        _baseSelecionada = BasesValor[0];
        _correcaoSelecionada = OpcoesCorrecao[0];
        _jurosSelecionado = OpcoesJuros[0];
        _acrescimosSelecionados = OpcoesAcrescimos[0];
        Historico = historico.Criar(this);
        // Por padrão, os 12 meses até o anterior ao atual, que já venceram.
        var ultima = DateTime.Today.AddMonths(-1);
        _ultimaParcela = ultima.ToString("MM/yyyy", Cultura);
        _primeiraParcela = ultima.AddMonths(-11).ToString("MM/yyyy", Cultura);
        _dataCalculo = DateTime.Today.ToString("dd/MM/yyyy", Cultura);
        Parcelas.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemParcelas));
        GerarParcelasCommand = new AsyncRelayCommand(GerarParcelasAsync);
        CalcularCommand = new AsyncRelayCommand(CalcularAsync);
        ExportarPdfCommand = new AsyncRelayCommand(ExportarPdfAsync, () => _ultimaSimulacao is not null);
        ExportarExcelCommand = new AsyncRelayCommand(
            () => ExportacaoPlanilha.SalvarAsync(_arquivoDialog, _notificador, $"pensao-em-atraso-{_ultimaSimulacao!.DataCalculo:yyyy-MM-dd}.xlsx",
                caminho => _planilha.GerarPensaoAtrasoAsync(_ultimaSimulacao!, caminho, CancellationToken.None)),
            () => _ultimaSimulacao is not null);
    }

    public IReadOnlyList<OpcaoCampo> BasesValor { get; } =
    [
        new("Valor fixo", BasePensao.ValorFixo),
        new("% do salário mínimo", BasePensao.SalarioMinimo)
    ];

    public OpcaoCampo BaseSelecionada
    {
        get => _baseSelecionada;
        set
        {
            if (SetProperty(ref _baseSelecionada, value))
            {
                OnPropertyChanged(nameof(EhValorFixo));
                OnPropertyChanged(nameof(EhSalarioMinimo));
                DesatualizarParcelas();
            }
        }
    }

    public bool EhValorFixo => (BasePensao)BaseSelecionada.Valor == BasePensao.ValorFixo;
    public bool EhSalarioMinimo => !EhValorFixo;
    public string ValorPensao { get => _valorPensao; set { if (SetProperty(ref _valorPensao, value)) DesatualizarParcelas(); } }
    public string Percentual { get => _percentual; set { if (SetProperty(ref _percentual, value)) DesatualizarParcelas(); } }
    public string DiaVencimento { get => _diaVencimento; set { if (SetProperty(ref _diaVencimento, value)) DesatualizarParcelas(); } }
    public string PrimeiraParcela { get => _primeiraParcela; set { if (SetProperty(ref _primeiraParcela, value)) DesatualizarParcelas(); } }
    public string UltimaParcela { get => _ultimaParcela; set { if (SetProperty(ref _ultimaParcela, value)) DesatualizarParcelas(); } }
    public string DataCalculo { get => _dataCalculo; set { if (SetProperty(ref _dataCalculo, value)) Desatualizar(); } }
    public string DataAjuizamento { get => _dataAjuizamento; set { if (SetProperty(ref _dataAjuizamento, value)) Desatualizar(); } }

    public IReadOnlyList<OpcaoCampo> OpcoesCorrecao { get; } =
    [
        new("INPC", CorrecaoMonetaria.Inpc),
        new("IPCA", CorrecaoMonetaria.Ipca),
        new("Sem correção", CorrecaoMonetaria.Nenhuma)
    ];
    public OpcaoCampo CorrecaoSelecionada { get => _correcaoSelecionada; set { if (SetProperty(ref _correcaoSelecionada, value)) Desatualizar(); } }

    public IReadOnlyList<OpcaoCampo> OpcoesJuros { get; } =
    [
        new("1% ao mês até 29/08/2024; depois, taxa legal", JurosDeMora.UmPorCentoAteTaxaLegal),
        new("1% ao mês", JurosDeMora.UmPorCento),
        new("Taxa legal (desde 30/08/2024)", JurosDeMora.TaxaLegal),
        new("Sem juros", JurosDeMora.Nenhum)
    ];
    public OpcaoCampo JurosSelecionado { get => _jurosSelecionado; set { if (SetProperty(ref _jurosSelecionado, value)) Desatualizar(); } }

    public IReadOnlyList<OpcaoCampo> OpcoesAcrescimos { get; } =
    [
        new("Não incluir", AcrescimosPenhora.Nenhum),
        new("Multa de 10% e honorários de 10%", AcrescimosPenhora.MultaEHonorarios),
        new("Só a multa de 10%", AcrescimosPenhora.Multa)
    ];
    public OpcaoCampo AcrescimosSelecionados { get => _acrescimosSelecionados; set { if (SetProperty(ref _acrescimosSelecionados, value)) Desatualizar(); } }

    public ObservableCollection<ParcelaAtrasoViewModel> Parcelas { get; } = [];
    public bool TemParcelas => Parcelas.Count > 0;
    public bool TemResultado { get => _temResultado; private set => SetProperty(ref _temResultado, value); }
    public IReadOnlyList<IndicadorResumoViewModel> IndicadoresResumo { get => _indicadoresResumo; private set => SetProperty(ref _indicadoresResumo, value); }
    public IReadOnlyList<string> Criterios { get => _criterios; private set => SetProperty(ref _criterios, value); }
    public IReadOnlyList<string> Observacoes { get => _observacoes; private set => SetProperty(ref _observacoes, value); }
    public ICommand GerarParcelasCommand { get; }
    public ICommand CalcularCommand { get; }
    public ICommand ExportarPdfCommand { get; }
    public ICommand ExportarExcelCommand { get; }
    public HistoricoDaJanela Historico { get; }

    public string TipoHistorico => "PensaoAtraso";
    public string NomeCalculadora => "Pensão em atraso";
    public string NomeSugerido => $"Pensão em atraso - cálculo em {DataCalculo}";

    public DadosFormulario ExportarDados() => new(
        new()
        {
            [nameof(BaseSelecionada)] = BaseSelecionada.Texto,
            [nameof(ValorPensao)] = ValorPensao,
            [nameof(Percentual)] = Percentual,
            [nameof(DiaVencimento)] = DiaVencimento,
            [nameof(PrimeiraParcela)] = PrimeiraParcela,
            [nameof(UltimaParcela)] = UltimaParcela,
            [nameof(DataCalculo)] = DataCalculo,
            [nameof(DataAjuizamento)] = DataAjuizamento,
            [nameof(CorrecaoSelecionada)] = CorrecaoSelecionada.Texto,
            [nameof(JurosSelecionado)] = JurosSelecionado.Texto,
            [nameof(AcrescimosSelecionados)] = AcrescimosSelecionados.Texto
        },
        Parcelas.Select(parcela => new Dictionary<string, string>
        {
            [nameof(ParcelaAtrasoViewModel.Mes)] = parcela.Mes,
            [nameof(ParcelaAtrasoViewModel.DataVencimento)] = parcela.DataVencimento,
            [nameof(ParcelaAtrasoViewModel.Devido)] = parcela.Devido,
            [nameof(ParcelaAtrasoViewModel.Pago)] = parcela.Pago
        }).ToList());

    public void ImportarDados(DadosFormulario dados)
    {
        BaseSelecionada = Opcao(BasesValor, dados.Valor(nameof(BaseSelecionada))) ?? BaseSelecionada;
        ValorPensao = dados.Valor(nameof(ValorPensao), ValorPensao);
        Percentual = dados.Valor(nameof(Percentual), Percentual);
        DiaVencimento = dados.Valor(nameof(DiaVencimento), DiaVencimento);
        PrimeiraParcela = dados.Valor(nameof(PrimeiraParcela), PrimeiraParcela);
        UltimaParcela = dados.Valor(nameof(UltimaParcela), UltimaParcela);
        DataCalculo = dados.Valor(nameof(DataCalculo), DataCalculo);
        DataAjuizamento = dados.Valor(nameof(DataAjuizamento), DataAjuizamento);
        CorrecaoSelecionada = Opcao(OpcoesCorrecao, dados.Valor(nameof(CorrecaoSelecionada))) ?? CorrecaoSelecionada;
        JurosSelecionado = Opcao(OpcoesJuros, dados.Valor(nameof(JurosSelecionado))) ?? JurosSelecionado;
        AcrescimosSelecionados = Opcao(OpcoesAcrescimos, dados.Valor(nameof(AcrescimosSelecionados))) ?? AcrescimosSelecionados;
        if (dados.Linhas is not { Count: > 0 } linhas)
            return;

        // As parcelas voltam como foram salvas, com os valores devidos e pagos digitados, sem gerar de novo.
        Parcelas.Clear();
        foreach (var linha in linhas)
        {
            if (!DateTime.TryParseExact(linha.GetValueOrDefault(nameof(ParcelaAtrasoViewModel.Mes)), "MM/yyyy", Cultura, DateTimeStyles.None, out var mes)
                || !DateOnly.TryParseExact(linha.GetValueOrDefault(nameof(ParcelaAtrasoViewModel.DataVencimento)), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var vencimento))
                continue;
            Parcelas.Add(new ParcelaAtrasoViewModel(new ParcelaPensaoInformada(DateOnly.FromDateTime(mes), vencimento, 0m, 0m), Desatualizar)
            {
                Devido = linha.GetValueOrDefault(nameof(ParcelaAtrasoViewModel.Devido), "0,00"),
                Pago = linha.GetValueOrDefault(nameof(ParcelaAtrasoViewModel.Pago), "0,00")
            });
        }
        _parcelasDesatualizadas = false;
    }

    public Task RecalcularAsync() => CalcularAsync();

    private static OpcaoCampo? Opcao(IReadOnlyList<OpcaoCampo> opcoes, string texto) => opcoes.FirstOrDefault(opcao => opcao.Texto == texto);

    private async Task GerarParcelasAsync()
    {
        if (!TentarLerMes(PrimeiraParcela, "primeira parcela", out var primeira) || !TentarLerMes(UltimaParcela, "última parcela", out var ultima))
            return;
        if (!int.TryParse(DiaVencimento.Trim(), out var dia) || !LeituraNumerica.TentarLer(ValorPensao, out var valor) || !LeituraNumerica.TentarLer(Percentual, out var percentual))
        {
            _notificador.MostrarAviso("Informe o dia do vencimento como número inteiro e o valor ou o percentual da pensão em formato válido.");
            return;
        }

        try
        {
            var geradas = await _simulador.GerarParcelasAsync(new GerarParcelasAtrasoRequest((BasePensao)BaseSelecionada.Valor, valor, percentual, primeira, ultima, dia), CancellationToken.None);
            if (geradas.Falhou)
            {
                _notificador.MostrarFalha(geradas.Erro, "Não foi possível gerar as parcelas.");
                return;
            }
            var parcelas = geradas.Valor;
            // Os pagamentos parciais já informados continuam nos mesmos meses.
            var pagos = Parcelas.GroupBy(linha => linha.Competencia).ToDictionary(grupo => grupo.Key, grupo => grupo.Last().Pago);
            Parcelas.Clear();
            foreach (var parcela in parcelas)
            {
                var linha = new ParcelaAtrasoViewModel(parcela, Desatualizar);
                if (pagos.TryGetValue(parcela.Competencia, out var pago))
                    linha.Pago = pago;
                Parcelas.Add(linha);
            }
            _parcelasDesatualizadas = false;
            Desatualizar();
        }
        catch (Exception ex) { _notificador.MostrarErro("Não foi possível gerar as parcelas.", ex); }
    }

    private async Task CalcularAsync()
    {
        // Sem parcelas geradas, ou com parcelas de outro valor ou período, o cálculo parte das parcelas do período informado.
        if (!TemParcelas || _parcelasDesatualizadas)
        {
            await GerarParcelasAsync();
            if (!TemParcelas || _parcelasDesatualizadas)
                return;
        }
        if (!DateOnly.TryParseExact(DataCalculo.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var dataCalculo))
        {
            _notificador.MostrarAviso("Informe a data do cálculo no formato dd/mm/aaaa.");
            return;
        }
        DateOnly? ajuizamento = null;
        if (!string.IsNullOrWhiteSpace(DataAjuizamento))
        {
            if (!DateOnly.TryParseExact(DataAjuizamento.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data))
            {
                _notificador.MostrarAviso("Informe a data do ajuizamento no formato dd/mm/aaaa, ou deixe o campo vazio se a execução ainda não foi proposta.");
                return;
            }
            ajuizamento = data;
        }

        var informadas = new List<ParcelaPensaoInformada>(Parcelas.Count);
        foreach (var linha in Parcelas)
        {
            if (!linha.TentarLer(out var parcela, out var erro))
            {
                _notificador.MostrarAviso(erro);
                return;
            }
            informadas.Add(parcela);
        }

        try
        {
            var simulacao = await _simulador.CalcularAsync(new SimularPensaoAtrasoRequest(informadas, dataCalculo, ajuizamento, (CorrecaoMonetaria)CorrecaoSelecionada.Valor, (JurosDeMora)JurosSelecionado.Valor,
                (AcrescimosPenhora)AcrescimosSelecionados.Valor), CancellationToken.None);
            if (simulacao.Falhou)
                _notificador.MostrarFalha(simulacao.Erro, "Não foi possível calcular a pensão em atraso.");
            else
                Apresentar(simulacao.Valor);
        }
        catch (Exception ex) { _notificador.MostrarErro("Não foi possível calcular a pensão em atraso.", ex); }
    }

    private void Apresentar(SimulacaoPensaoAtrasoDto simulacao)
    {
        _ultimaSimulacao = simulacao;
        // A ordem das parcelas na tela é a mesma do resultado: por vencimento.
        var resultados = simulacao.Parcelas.ToDictionary(parcela => (parcela.Competencia, parcela.Vencimento));
        foreach (var linha in Parcelas)
            linha.Apresentar(resultados.GetValueOrDefault((linha.Competencia, linha.Vencimento)));

        IndicadoresResumo =
        [
            new("Total atualizado", Moeda(simulacao.TotalComAcrescimos), simulacao.TemAcrescimos ? $"Em {simulacao.DataCalculo:dd/MM/yyyy}, com {Acrescimos(simulacao)}" : $"Em {simulacao.DataCalculo:dd/MM/yyyy}"),
            new("Rito da prisão", Moeda(simulacao.TotalPrisao), TextoParcelas(simulacao.QuantidadePrisao)),
            new("Rito da penhora", Moeda(simulacao.TotalPenhoraComAcrescimos), simulacao.TemAcrescimos ? $"{TextoParcelas(simulacao.QuantidadePenhora)} e {Moeda(simulacao.Multa + simulacao.Honorarios)} de {Acrescimos(simulacao)}" : TextoParcelas(simulacao.QuantidadePenhora)),
            new("Saldo original", Moeda(simulacao.TotalSaldo), simulacao.TotalPago > 0m ? $"Devido menos {Moeda(simulacao.TotalPago)} pagos" : "Sem pagamentos parciais"),
            new("Correção", Moeda(simulacao.TotalCorrecao), simulacao.CorrecaoAte is { } ate ? $"{SimulacaoPensaoAtrasoDto.NomeCorrecao(simulacao.Correcao)} até {ate:MM/yyyy}" : "Sem correção"),
            new("Juros de mora", Moeda(simulacao.TotalJuros), JurosSelecionado.Texto)
        ];
        Criterios = simulacao.Criterios;
        Observacoes = simulacao.Observacoes;
        TemResultado = true;
        ((AsyncRelayCommand)ExportarPdfCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportarExcelCommand).RaiseCanExecuteChanged();
    }

    /// <summary>Ao mudar o valor, o percentual, o dia ou o período, as parcelas da lista são geradas de novo no próximo cálculo.</summary>
    private void DesatualizarParcelas()
    {
        if (TemParcelas)
            _parcelasDesatualizadas = true;
        Desatualizar();
    }

    /// <summary>Ao mudar uma parcela ou um critério, o resultado anterior deixa de valer até o próximo cálculo.</summary>
    private void Desatualizar()
    {
        if (_ultimaSimulacao is null && !TemResultado)
            return;
        _ultimaSimulacao = null;
        foreach (var linha in Parcelas)
            linha.Apresentar(null);
        TemResultado = false;
        ((AsyncRelayCommand)ExportarPdfCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportarExcelCommand).RaiseCanExecuteChanged();
    }

    private bool TentarLerMes(string texto, string campo, out DateOnly mes)
    {
        mes = default;
        if (DateTime.TryParseExact(texto.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data))
        {
            mes = DateOnly.FromDateTime(data);
            return true;
        }
        _notificador.MostrarAviso($"Informe a {campo} no formato mm/aaaa.");
        return false;
    }

    private async Task ExportarPdfAsync()
    {
        if (_ultimaSimulacao is null)
            return;

        var caminhoArquivo = _arquivoDialog.SolicitarDestinoPdf($"pensao-em-atraso-{_ultimaSimulacao.DataCalculo:yyyy-MM-dd}.pdf");
        if (caminhoArquivo is null)
            return;

        try
        {
            await _relatorioPdf.GerarRelatorioPensaoAtrasoAsync(_ultimaSimulacao, caminhoArquivo, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro("Não foi possível gerar o relatório em PDF.", exception);
        }
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", Cultura);
    private static string TextoParcelas(int quantidade) => quantidade == 1 ? "1 parcela" : $"{quantidade} parcelas";
    private static string Acrescimos(SimulacaoPensaoAtrasoDto simulacao) => simulacao.Honorarios > 0m ? "multa e honorários" : "multa";
}
