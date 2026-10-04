using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.Services;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using System.Globalization;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed class EstabilidadeViewModel : ViewModelBase, ICalculoSalvavel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly ISimularEstabilidadeUseCase _simulador;
    private readonly IUserNotifier _notificador;
    private readonly IRelatorioPdfService _relatorioPdf;
    private readonly IPlanilhaService _planilha;
    private readonly IArquivoDialogService _arquivoDialog;
    private string _mediaRemuneratoria = "0,00";
    private string _diasBase = "30";
    private string _demissao = DateTime.Today.ToString("dd/MM/yyyy", Cultura);
    private string _fimEstabilidade = DateTime.Today.AddDays(30).ToString("dd/MM/yyyy", Cultura);
    private string _complementos = "0,00";
    private string _diasRestantes = "30 dias restantes";
    private bool _temResultado;
    private IReadOnlyList<IndicadorResumoViewModel> _indicadoresResumo = [];
    private IReadOnlyList<FormulaCalculoViewModel> _memoriaCalculo = [];
    private SimulacaoEstabilidadeDto? _ultimaSimulacao;
    private EntradaEstabilidadeDto? _ultimaEntrada;

    public EstabilidadeViewModel(ISimularEstabilidadeUseCase simulador, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha, IArquivoDialogService arquivoDialog,
        IHistoricoDaJanelaFactory historico)
    {
        _simulador = simulador;
        _notificador = notificador;
        _relatorioPdf = relatorioPdf;
        _planilha = planilha;
        _arquivoDialog = arquivoDialog;
        CalcularCommand = new RelayCommand(_ => Calcular());
        Historico = historico.Criar(this);
        ExportarPdfCommand = new AsyncRelayCommand(ExportarPdfAsync, () => _ultimaSimulacao is not null && _ultimaEntrada is not null);
        ExportarExcelCommand = new AsyncRelayCommand(
            () => ExportacaoPlanilha.SalvarAsync(_arquivoDialog, _notificador, $"estabilidade-{_ultimaEntrada!.Demissao:yyyy-MM-dd}.xlsx",
                caminho => _planilha.GerarEstabilidadeAsync(_ultimaSimulacao!, _ultimaEntrada!, caminho, CancellationToken.None)),
            () => _ultimaSimulacao is not null && _ultimaEntrada is not null);
    }

    public string MediaRemuneratoria { get => _mediaRemuneratoria; set => SetProperty(ref _mediaRemuneratoria, value); }
    public string DiasBase { get => _diasBase; set => SetProperty(ref _diasBase, value); }
    public string Demissao
    {
        get => _demissao;
        set
        {
            if (SetProperty(ref _demissao, value))
                AtualizarDiasRestantes();
        }
    }
    public string FimEstabilidade
    {
        get => _fimEstabilidade;
        set
        {
            if (SetProperty(ref _fimEstabilidade, value))
                AtualizarDiasRestantes();
        }
    }
    public string Complementos { get => _complementos; set => SetProperty(ref _complementos, value); }
    public string DiasRestantes { get => _diasRestantes; private set => SetProperty(ref _diasRestantes, value); }
    public bool TemResultado { get => _temResultado; private set => SetProperty(ref _temResultado, value); }
    public IReadOnlyList<IndicadorResumoViewModel> IndicadoresResumo { get => _indicadoresResumo; private set => SetProperty(ref _indicadoresResumo, value); }
    public IReadOnlyList<FormulaCalculoViewModel> MemoriaCalculo { get => _memoriaCalculo; private set => SetProperty(ref _memoriaCalculo, value); }
    public ICommand CalcularCommand { get; }
    public ICommand ExportarPdfCommand { get; }
    public ICommand ExportarExcelCommand { get; }
    public HistoricoDaJanela Historico { get; }

    public string TipoHistorico => "Estabilidade";
    public string NomeCalculadora => "Estabilidade";
    public string NomeSugerido => $"Estabilidade - demissão em {Demissao}";

    public DadosFormulario ExportarDados() => new(new()
    {
        [nameof(MediaRemuneratoria)] = MediaRemuneratoria,
        [nameof(DiasBase)] = DiasBase,
        [nameof(Demissao)] = Demissao,
        [nameof(FimEstabilidade)] = FimEstabilidade,
        [nameof(Complementos)] = Complementos
    });

    public void ImportarDados(DadosFormulario dados)
    {
        MediaRemuneratoria = dados.Valor(nameof(MediaRemuneratoria), MediaRemuneratoria);
        DiasBase = dados.Valor(nameof(DiasBase), DiasBase);
        Demissao = dados.Valor(nameof(Demissao), Demissao);
        FimEstabilidade = dados.Valor(nameof(FimEstabilidade), FimEstabilidade);
        Complementos = dados.Valor(nameof(Complementos), Complementos);
    }

    public Task RecalcularAsync()
    {
        Calcular();
        return Task.CompletedTask;
    }

    private void Calcular()
    {
        if (!TentarLerEntrada(out var entrada))
            return;

        try
        {
            var simulacao = _simulador.Executar(entrada);
            if (simulacao.Falhou)
            {
                _notificador.MostrarFalha(simulacao.Erro, "Não foi possível concluir o cálculo de estabilidade.");
                return;
            }
            var resultado = simulacao.Valor;
            _ultimaSimulacao = resultado;
            _ultimaEntrada = new EntradaEstabilidadeDto(entrada.MediaRemuneratoria, entrada.DiasBase, entrada.Demissao, entrada.FimEstabilidade, entrada.Complementos);
            DiasRestantes = $"{resultado.DiasEstabilidade:N0} dias restantes";
            IndicadoresResumo =
            [
                new("Indenização", Moeda(resultado.Indenizacao), Periodo(resultado)),
                new("13º salário", Moeda(resultado.DecimoTerceiro), $"{resultado.AvosDecimoTerceiro} avo(s)"),
                new("Férias + 1/3", Moeda(resultado.Ferias + resultado.TercoFerias), $"{resultado.AvosFerias} avo(s); férias: {Moeda(resultado.Ferias)}"),
                new("FGTS + multa", Moeda(resultado.FgtsOitoPorCento + resultado.MultaFgtsQuarentaPorCento), "8% + multa de 40%"),
                new("Complementos", Moeda(resultado.Complementos), "Ajuste adicional informado"),
                new("Total estimado", Moeda(resultado.Total), "Soma das verbas calculadas")
            ];
            MemoriaCalculo =
            [
                new("Período", $"{resultado.DiasEstabilidade:N0} dias após a demissão: {Periodo(resultado)}"),
                new("Indenização", FormulaIndenizacao(entrada, resultado)),
                new("13º salário", $"{Moeda(entrada.MediaRemuneratoria)} ÷ 12 × {resultado.AvosDecimoTerceiro} avo(s) = {Moeda(resultado.DecimoTerceiro)}: meses do ano com 15 dias ou mais de contrato, além dos pagos na rescisão"),
                new("Férias proporcionais", $"{Moeda(entrada.MediaRemuneratoria)} ÷ 12 × {resultado.AvosFerias} avo(s) = {Moeda(resultado.Ferias)}"),
                new("Adicional de férias", $"{Moeda(resultado.Ferias)} ÷ 3 = {Moeda(resultado.TercoFerias)}"),
                new("FGTS", $"({Moeda(resultado.Indenizacao)} + {Moeda(resultado.DecimoTerceiro)}) × 8% = {Moeda(resultado.FgtsOitoPorCento)}"),
                new("Multa rescisória do FGTS", $"{Moeda(resultado.FgtsOitoPorCento)} × 40% = {Moeda(resultado.MultaFgtsQuarentaPorCento)}"),
                new("Total estimado", $"Verbas calculadas + complementos de {Moeda(resultado.Complementos)} = {Moeda(resultado.Total)}")
            ];
            TemResultado = true;
            ((AsyncRelayCommand)ExportarPdfCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)ExportarExcelCommand).RaiseCanExecuteChanged();
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro("Não foi possível concluir o cálculo de estabilidade.", exception);
        }
    }

    private bool TentarLerEntrada(out SimularEstabilidadeRequest entrada)
    {
        entrada = default!;
        if (!LeituraNumerica.TentarLer(MediaRemuneratoria, out var media) ||
            !int.TryParse(DiasBase, out var diasBase) ||
            !DateOnly.TryParseExact(Demissao.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var demissao) ||
            !DateOnly.TryParseExact(FimEstabilidade.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var fimEstabilidade) ||
            !LeituraNumerica.TentarLer(Complementos, out var complementos))
        {
            _notificador.MostrarAviso("Informe média, dias-base, datas (dd/MM/aaaa) e complementos em formatos válidos.");
            return false;
        }

        entrada = new SimularEstabilidadeRequest(media, diasBase, demissao, fimEstabilidade, complementos);
        return true;
    }

    private async Task ExportarPdfAsync()
    {
        if (_ultimaSimulacao is null || _ultimaEntrada is null)
            return;

        var caminhoArquivo = _arquivoDialog.SolicitarDestinoPdf($"demonstrativo-estabilidade-{_ultimaEntrada.Demissao:yyyy-MM-dd}.pdf");
        if (caminhoArquivo is null)
            return;

        try
        {
            await _relatorioPdf.GerarRelatorioEstabilidadeAsync(_ultimaSimulacao, _ultimaEntrada, caminhoArquivo, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro("Não foi possível gerar o demonstrativo de estabilidade.", exception);
        }
    }

    private void AtualizarDiasRestantes()
    {
        if (DateOnly.TryParseExact(Demissao.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var demissao) &&
            DateOnly.TryParseExact(FimEstabilidade.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var fimEstabilidade))
        {
            var dias = fimEstabilidade.DayNumber - demissao.DayNumber;
            DiasRestantes = dias > 0 ? $"{dias:N0} dias restantes" : "A data final deve ser posterior à demissão";
            return;
        }

        DiasRestantes = "Informe as duas datas";
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", Cultura);

    private static string Periodo(SimulacaoEstabilidadeDto resultado)
    {
        var meses = resultado.Meses == 1 ? "1 mês" : $"{resultado.Meses} meses";
        var dias = resultado.DiasAlemDosMeses == 1 ? "1 dia" : $"{resultado.DiasAlemDosMeses} dias";
        return resultado.Meses == 0 ? dias : resultado.DiasAlemDosMeses == 0 ? meses : $"{meses} e {dias}";
    }

    // Os meses cheios valem a média; os dias que sobram, a média diária pelos dias-base.
    private static string FormulaIndenizacao(SimularEstabilidadeRequest entrada, SimulacaoEstabilidadeDto resultado)
    {
        var termos = new List<string>();
        if (resultado.Meses > 0)
            termos.Add($"{Moeda(entrada.MediaRemuneratoria)} × {resultado.Meses} {(resultado.Meses == 1 ? "mês" : "meses")}");
        if (resultado.DiasAlemDosMeses > 0)
            termos.Add($"{Moeda(entrada.MediaRemuneratoria)} ÷ {entrada.DiasBase} × {resultado.DiasAlemDosMeses} {(resultado.DiasAlemDosMeses == 1 ? "dia" : "dias")}");
        return $"{string.Join(" + ", termos)} = {Moeda(resultado.Indenizacao)}";
    }
}
