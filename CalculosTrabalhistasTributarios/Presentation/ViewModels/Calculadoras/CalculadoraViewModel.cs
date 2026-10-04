using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.Services;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using System.Globalization;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Janela padrão das calculadoras: formulário, demonstrativo no formato de holerite, memória de cálculo e PDF.</summary>
public sealed class CalculadoraViewModel : ViewModelBase, ICalculoSalvavel
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly ICalculadora _calculadora;
    private readonly TipoCalculadora _tipo;
    private readonly IUserNotifier _notificador;
    private readonly IRelatorioPdfService _relatorioPdf;
    private readonly IPlanilhaService _planilha;
    private readonly IArquivoDialogService _arquivoDialog;
    private readonly ContextoCompartilhado _contexto;
    private DemonstrativoDto? _demonstrativo;
    private string _nomeArquivoPdf = string.Empty;
    private bool _temResultado;
    private string _referencia = string.Empty;
    private IReadOnlyList<IndicadorResumoViewModel> _destaques = [];
    private IReadOnlyList<LinhaDemonstrativoViewModel> _linhas = [];
    private IReadOnlyList<LinhaDemonstrativoViewModel> _informativos = [];
    private IReadOnlyList<SecaoMemoriaIrrfViewModel> _memoria = [];
    private IReadOnlyList<string> _observacoes = [];
    private string _totalProventos = string.Empty;
    private string _totalDescontos = string.Empty;
    private string _resultado = string.Empty;
    private string _rotuloProventos = "Proventos";
    private string _rotuloResultado = "Líquido a receber";
    private bool _temDemonstrativo = true;
    private string _tituloComparativo = string.Empty;
    private IReadOnlyList<string> _colunasComparativo = [];
    private IReadOnlyList<LinhaComparativaViewModel> _linhasComparativo = [];

    public CalculadoraViewModel(TipoCalculadora tipo, ICalculadora calculadora, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha, IArquivoDialogService arquivoDialog,
        ContextoCompartilhado contexto, IHistoricoDaJanelaFactory historico)
    {
        _tipo = tipo;
        _calculadora = calculadora;
        _notificador = notificador;
        _relatorioPdf = relatorioPdf;
        _planilha = planilha;
        _arquivoDialog = arquivoDialog;
        _contexto = contexto;
        Campos = calculadora.Campos;
        Historico = historico.Criar(this);
        CalcularCommand = new AsyncRelayCommand(CalcularAsync);
        ExportarPdfCommand = new AsyncRelayCommand(ExportarPdfAsync, () => _demonstrativo is not null);
        ExportarExcelCommand = new AsyncRelayCommand(
            () => ExportacaoPlanilha.SalvarAsync(_arquivoDialog, _notificador, _nomeArquivoPdf, caminho => _planilha.GerarDemonstrativoAsync(_demonstrativo!, caminho, CancellationToken.None)),
            () => _demonstrativo is not null);
    }

    public string Titulo => _calculadora.Titulo;
    public string Descricao => _calculadora.Descricao;
    public string InstrucaoInicial => _calculadora.InstrucaoInicial;
    public IReadOnlyList<CampoViewModel> Campos { get; }
    public ICommand CalcularCommand { get; }
    public ICommand ExportarPdfCommand { get; }
    public ICommand ExportarExcelCommand { get; }
    public HistoricoDaJanela Historico { get; }

    public string TipoHistorico => $"Calculadora.{_tipo}";
    public string NomeCalculadora => Titulo;
    public string NomeSugerido => _calculadora.Contexto.Competencia is { } competencia ? $"{Titulo} - {competencia:MM/yyyy}" : $"{Titulo} - {DateTime.Today:dd/MM/yyyy}";
    public DadosFormulario ExportarDados() => new(_calculadora.ExportarCampos());
    public void ImportarDados(DadosFormulario dados) => _calculadora.ImportarCampos(dados.Campos);
    public Task RecalcularAsync() => CalcularAsync();

    /// <summary>Preenche campos vindos de outra janela, como as horas apuradas na jornada, sem calcular.</summary>
    public void ImportarCampos(IReadOnlyDictionary<string, string> valores) => _calculadora.ImportarCampos(valores);

    public bool TemResultado { get => _temResultado; private set => SetProperty(ref _temResultado, value); }
    public string Referencia { get => _referencia; private set => SetProperty(ref _referencia, value); }
    public IReadOnlyList<IndicadorResumoViewModel> Destaques { get => _destaques; private set => SetProperty(ref _destaques, value); }
    public IReadOnlyList<LinhaDemonstrativoViewModel> Linhas { get => _linhas; private set => SetProperty(ref _linhas, value); }
    public IReadOnlyList<LinhaDemonstrativoViewModel> Informativos { get => _informativos; private set { SetProperty(ref _informativos, value); OnPropertyChanged(nameof(TemInformativos)); } }
    public bool TemInformativos => Informativos.Count > 0;
    public IReadOnlyList<SecaoMemoriaIrrfViewModel> Memoria { get => _memoria; private set => SetProperty(ref _memoria, value); }
    public IReadOnlyList<string> Observacoes { get => _observacoes; private set { SetProperty(ref _observacoes, value); OnPropertyChanged(nameof(TemObservacoes)); } }
    public bool TemObservacoes => Observacoes.Count > 0;
    public string TotalProventos { get => _totalProventos; private set => SetProperty(ref _totalProventos, value); }
    public string TotalDescontos { get => _totalDescontos; private set { SetProperty(ref _totalDescontos, value); OnPropertyChanged(nameof(TemDescontos)); } }
    public bool TemDescontos => TotalDescontos.Length > 0;
    public string Resultado { get => _resultado; private set => SetProperty(ref _resultado, value); }
    public string RotuloProventos { get => _rotuloProventos; private set => SetProperty(ref _rotuloProventos, value); }
    public string RotuloResultado { get => _rotuloResultado; private set => SetProperty(ref _rotuloResultado, value); }

    /// <summary>Falso quando o cálculo não tem proventos nem descontos, como uma comparação de cenários.</summary>
    public bool TemDemonstrativo { get => _temDemonstrativo; private set => SetProperty(ref _temDemonstrativo, value); }
    public string TituloComparativo { get => _tituloComparativo; private set => SetProperty(ref _tituloComparativo, value); }
    public IReadOnlyList<string> ColunasComparativo { get => _colunasComparativo; private set => SetProperty(ref _colunasComparativo, value); }
    public IReadOnlyList<LinhaComparativaViewModel> LinhasComparativo { get => _linhasComparativo; private set { SetProperty(ref _linhasComparativo, value); OnPropertyChanged(nameof(TemComparativo)); } }
    public bool TemComparativo => LinhasComparativo.Count > 0;

    private async Task CalcularAsync()
    {
        try
        {
            var resultado = await _calculadora.CalcularAsync(CancellationToken.None);
            if (resultado.Falhou)
            {
                _notificador.MostrarFalha(resultado.Erro, $"Não foi possível concluir o cálculo de {Titulo.ToLower(Cultura)}.");
                return;
            }
            _nomeArquivoPdf = _calculadora.NomeArquivoPdf;
            _contexto.Registrar(_calculadora.Contexto);
            Apresentar(resultado.Valor);
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro($"Não foi possível concluir o cálculo de {Titulo.ToLower(Cultura)}.", exception);
        }
    }

    private void Apresentar(DemonstrativoDto demonstrativo)
    {
        _demonstrativo = demonstrativo;
        Referencia = demonstrativo.Referencia;
        Destaques = demonstrativo.Destaques.Select(destaque => new IndicadorResumoViewModel(destaque.Rotulo, destaque.Valor, destaque.Complemento)).ToArray();
        Linhas = demonstrativo.Proventos.Select(verba => new LinhaDemonstrativoViewModel(verba.Descricao, verba.Referencia, Moeda(verba.Valor), string.Empty))
            .Concat(demonstrativo.Descontos.Select(verba => new LinhaDemonstrativoViewModel(verba.Descricao, verba.Referencia, string.Empty, Moeda(verba.Valor))))
            .ToArray();
        Informativos = demonstrativo.Informativos.Select(verba => new LinhaDemonstrativoViewModel(verba.Descricao, verba.Referencia, Moeda(verba.Valor), string.Empty)).ToArray();
        Memoria = demonstrativo.Memoria.Select(grupo => new SecaoMemoriaIrrfViewModel(grupo.Titulo, grupo.Destaque, grupo.Formulas.Select(formula => new FormulaCalculoViewModel(formula.Titulo, formula.Formula)).ToArray())).ToArray();
        Observacoes = demonstrativo.Observacoes;
        RotuloProventos = demonstrativo.RotuloProventos;
        RotuloResultado = demonstrativo.RotuloResultado;
        TotalProventos = Moeda(demonstrativo.TotalProventos);
        TotalDescontos = demonstrativo.Descontos.Count > 0 ? Moeda(demonstrativo.TotalDescontos) : string.Empty;
        Resultado = Moeda(demonstrativo.Resultado);
        TemDemonstrativo = demonstrativo.TemVerbas;
        TituloComparativo = demonstrativo.Comparativo?.Titulo ?? string.Empty;
        ColunasComparativo = demonstrativo.Comparativo?.Colunas ?? [];
        LinhasComparativo = demonstrativo.Comparativo?.Linhas.Select(linha => new LinhaComparativaViewModel(linha.Descricao, linha.Valores, linha.Destaque)).ToArray() ?? [];
        TemResultado = true;
        ((AsyncRelayCommand)ExportarPdfCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportarExcelCommand).RaiseCanExecuteChanged();
    }

    private async Task ExportarPdfAsync()
    {
        if (_demonstrativo is null)
            return;

        var caminhoArquivo = _arquivoDialog.SolicitarDestinoPdf(_nomeArquivoPdf);
        if (caminhoArquivo is null)
            return;

        try
        {
            await _relatorioPdf.GerarDemonstrativoAsync(_demonstrativo, caminhoArquivo, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro("Não foi possível gerar o relatório em PDF.", exception);
        }
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", Cultura);
}
