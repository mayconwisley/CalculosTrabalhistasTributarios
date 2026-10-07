using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.Services;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using System.ComponentModel;
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
    private Action<TipoCalculadora, IReadOnlyDictionary<string, string>>? _abrirCalculadora;
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
    private string _aviso = string.Empty;
    private bool _avisoDosCampos;
    private bool _resultadoDesatualizado;
    private IReadOnlyDictionary<string, string>? _dadosDoResultado;
    private IReadOnlyDictionary<string, string>? _dadosSalvos;

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
        Historico.Sincronizado += MarcarComoSalvo;
        foreach (var campo in Campos)
            campo.PropertyChanged += AoAlterarCampo;
        CalcularCommand = new AsyncRelayCommand(() => CalcularAsync(solicitadoPeloUsuario: true));
        ExportarPdfCommand = new AsyncRelayCommand(ExportarPdfAsync, () => _demonstrativo is not null);
        ExportarExcelCommand = new AsyncRelayCommand(
            () => ExportacaoPlanilha.SalvarAsync(_arquivoDialog, _notificador, _nomeArquivoPdf, caminho => _planilha.GerarDemonstrativoAsync(_demonstrativo!, caminho, CancellationToken.None)),
            () => _demonstrativo is not null);
        UsarNoHoleriteCommand = new RelayCommand(_ => TransferirQuitacao(TipoCalculadora.Holerite),
            _ => _demonstrativo?.QuitacaoBancoHoras?.Situacao == SituacaoBancoHoras.Fechamento && _abrirCalculadora is not null);
        UsarNaRescisaoCommand = new RelayCommand(_ => TransferirQuitacao(TipoCalculadora.Rescisao),
            _ => _demonstrativo?.QuitacaoBancoHoras?.Situacao == SituacaoBancoHoras.Rescisao && _abrirCalculadora is not null);
        CalcularRraCommand = new RelayCommand(_ => AbrirRra(), _ => PodeCalcularRra && _abrirCalculadora is not null);
        LevarFgtsRescisaoCommand = new RelayCommand(_ => LevarFgtsRescisao(), _ => PodeLevarFgtsRescisao && _abrirCalculadora is not null);
    }

    public string Titulo => _calculadora.Titulo;
    public string Descricao => _calculadora.Descricao;
    public string InstrucaoInicial => _calculadora.InstrucaoInicial;
    public IReadOnlyList<CampoViewModel> Campos { get; }
    public bool UsaFormularioLarguraTotal => Campos.Count == 1 && Campos[0] is CampoBancoHorasViewModel;
    public bool UsaBotaoAbaixoFormulario => UsaFormularioLarguraTotal || Campos.Any(campo => campo is CampoDepositosFgtsViewModel or CampoQuitacaoBancoHorasViewModel or CampoParcelasRraViewModel or CampoMesesSimplesViewModel or CampoConferenciaFgtsViewModel);
    public ICommand CalcularCommand { get; }
    public ICommand ExportarPdfCommand { get; }
    public ICommand ExportarExcelCommand { get; }
    public ICommand UsarNoHoleriteCommand { get; }
    public ICommand UsarNaRescisaoCommand { get; }
    public ICommand CalcularRraCommand { get; }
    public ICommand LevarFgtsRescisaoCommand { get; }
    public bool PodeUsarNoHolerite => _demonstrativo?.QuitacaoBancoHoras?.Situacao == SituacaoBancoHoras.Fechamento;
    public bool PodeUsarNaRescisao => _demonstrativo?.QuitacaoBancoHoras?.Situacao == SituacaoBancoHoras.Rescisao;
    public bool PodeCalcularRra => _demonstrativo?.ParcelasRra is { Count: > 0 };
    public bool PodeLevarFgtsRescisao => _demonstrativo?.DepositosFgts is { Depositos.Count: > 0 };
    public HistoricoDaJanela Historico { get; }

    public string TipoHistorico => $"Calculadora.{_tipo}";
    public string NomeCalculadora => Titulo;
    public string NomeSugerido => _calculadora.Contexto.Competencia is { } competencia ? $"{Titulo} - {competencia:MM/yyyy}" : $"{Titulo} - {DateTime.Today:dd/MM/yyyy}";
    public DadosFormulario ExportarDados() => new(_calculadora.ExportarCampos());
    public void ImportarDados(DadosFormulario dados) => _calculadora.ImportarCampos(dados.Campos);
    public Task RecalcularAsync() => CalcularAsync(solicitadoPeloUsuario: false);

    /// <summary>Preenche campos vindos de outra janela, como as horas apuradas na jornada, sem calcular.</summary>
    public void ImportarCampos(IReadOnlyDictionary<string, string> valores) => _calculadora.ImportarCampos(valores);

    public void ConfigurarAberturaCalculadora(Action<TipoCalculadora, IReadOnlyDictionary<string, string>> abrirCalculadora)
    {
        _abrirCalculadora = abrirCalculadora;
        ((RelayCommand)UsarNoHoleriteCommand).RaiseCanExecuteChanged();
        ((RelayCommand)UsarNaRescisaoCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CalcularRraCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LevarFgtsRescisaoCommand).RaiseCanExecuteChanged();
    }

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

    /// <summary>Por que o último cálculo não foi feito, exibido junto do botão Calcular; vazio quando não há aviso.</summary>
    public string Aviso { get => _aviso; private set { SetProperty(ref _aviso, value); OnPropertyChanged(nameof(TemAviso)); } }
    public bool TemAviso => Aviso.Length > 0;

    /// <summary>O formulário mudou depois do cálculo: o resultado exibido não corresponde mais aos dados digitados.</summary>
    public bool ResultadoDesatualizado { get => _resultadoDesatualizado; private set => SetProperty(ref _resultadoDesatualizado, value); }

    /// <summary>Um cálculo pedido pelo usuário terminou; a janela rola até o resultado.</summary>
    public event Action? ResultadoApresentado;

    /// <summary>Um cálculo pedido pelo usuário parou num campo inválido; a janela leva o foco até ele.</summary>
    public event Action<CampoViewModel>? CampoComErro;

    /// <summary>Confere se o formulário ainda corresponde ao resultado; a janela chama a cada edição, inclusive nas grades.</summary>
    public void VerificarAlteracoes()
    {
        if (_dadosDoResultado is not null && TemResultado)
            ResultadoDesatualizado = !EquivalenciaFormulario.Equivalentes(_dadosDoResultado, _calculadora.ExportarCampos());
    }

    /// <summary>Registra o formulário atual como o salvo: ao abrir a janela, ao salvar e ao abrir do histórico.</summary>
    public void MarcarComoSalvo() => _dadosSalvos = _calculadora.ExportarCampos();

    public bool TemAlteracoesNaoSalvas => _dadosSalvos is not null && !EquivalenciaFormulario.Equivalentes(_dadosSalvos, _calculadora.ExportarCampos());

    /// <summary>Pede confirmação antes de fechar pelo Esc quando há dados que não estão no histórico.</summary>
    public bool ConfirmarFechamento() => !TemAlteracoesNaoSalvas ||
        _notificador.Confirmar("Os dados deste formulário não foram salvos no histórico e serão perdidos ao fechar a janela.\n\nFechar mesmo assim?", "Fechar a calculadora");

    private void AoAlterarCampo(object? sender, PropertyChangedEventArgs argumentos)
    {
        if (argumentos.PropertyName is nameof(CampoTextoViewModel.Valor) or nameof(CampoOpcaoViewModel.Selecionada))
            VerificarAlteracoes();
        // Corrigidos todos os campos apontados, o aviso deles sai; um aviso sem campo fica até o próximo cálculo.
        else if (argumentos.PropertyName == nameof(CampoViewModel.MensagemErro) && _avisoDosCampos && !Campos.Any(campo => campo.TemErro))
            LimparAviso();
    }

    private async Task CalcularAsync(bool solicitadoPeloUsuario)
    {
        LimparAviso();
        foreach (var campo in Campos)
            campo.MensagemErro = null;
        try
        {
            var resultado = await _calculadora.CalcularAsync(CancellationToken.None);
            if (resultado.Falhou)
            {
                ApresentarFalha(resultado.Erro, solicitadoPeloUsuario);
                return;
            }
            _nomeArquivoPdf = _calculadora.NomeArquivoPdf;
            _contexto.Registrar(_calculadora.Contexto);
            Apresentar(resultado.Valor);
            _dadosDoResultado = _calculadora.ExportarCampos();
            ResultadoDesatualizado = false;
            if (solicitadoPeloUsuario)
                ResultadoApresentado?.Invoke();
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro($"Não foi possível concluir o cálculo de {Titulo.ToLower(Cultura)}.", exception);
        }
    }

    // Erros de preenchimento ficam no formulário, junto dos campos; falhas de tabela ou de serviço continuam numa mensagem.
    private void ApresentarFalha(Erro erro, bool solicitadoPeloUsuario)
    {
        if (erro.Tipo != TipoErro.Validacao)
        {
            _notificador.MostrarFalha(erro, $"Não foi possível concluir o cálculo de {Titulo.ToLower(Cultura)}.");
            return;
        }

        var marcados = Campos.Where(campo => campo.Visivel && campo.TemErro).ToList();
        if (marcados.Count == 0 && CampoCitado(erro.Mensagem) is { } citado)
        {
            citado.MensagemErro = "Confira este campo.";
            marcados.Add(citado);
        }
        Aviso = marcados.Count > 1
            ? $"Corrija os {marcados.Count} campos destacados: {Lista(marcados.Select(campo => campo.Rotulo))}."
            : erro.Mensagem;
        _avisoDosCampos = marcados.Count > 0;
        if (solicitadoPeloUsuario && marcados.Count > 0)
            CampoComErro?.Invoke(marcados[0]);
    }

    private void LimparAviso()
    {
        Aviso = string.Empty;
        _avisoDosCampos = false;
    }

    /// <summary>
    /// O campo que a mensagem de validação cita pelo rótulo, como em "Valor solicitado: informe..." ou em "O campo
    /// "Salário" ..."; o complemento do rótulo entre parênteses, como a unidade, é desconsiderado.
    /// </summary>
    private CampoViewModel? CampoCitado(string mensagem)
    {
        foreach (var campo in Campos.Where(campo => campo.Visivel && campo is CampoTextoViewModel or CampoOpcaoViewModel))
        {
            var rotulo = campo.Rotulo.Split(" (")[0].Trim();
            if (rotulo.Length == 0)
                continue;
            if (mensagem.StartsWith(rotulo + ":", StringComparison.CurrentCultureIgnoreCase)
                || mensagem.Contains($"\"{rotulo}", StringComparison.CurrentCultureIgnoreCase)
                || mensagem.Contains($"“{rotulo}", StringComparison.CurrentCultureIgnoreCase))
                return campo;
        }
        return null;
    }

    private static string Lista(IEnumerable<string> itens)
    {
        var lista = itens.ToArray();
        return lista.Length == 1 ? lista[0] : $"{string.Join(", ", lista[..^1])} e {lista[^1]}";
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
        OnPropertyChanged(nameof(PodeUsarNoHolerite));
        OnPropertyChanged(nameof(PodeUsarNaRescisao));
        OnPropertyChanged(nameof(PodeCalcularRra));
        OnPropertyChanged(nameof(PodeLevarFgtsRescisao));
        ((RelayCommand)UsarNoHoleriteCommand).RaiseCanExecuteChanged();
        ((RelayCommand)UsarNaRescisaoCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CalcularRraCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LevarFgtsRescisaoCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportarPdfCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportarExcelCommand).RaiseCanExecuteChanged();
    }

    private void TransferirQuitacao(TipoCalculadora destino)
    {
        if (_demonstrativo?.QuitacaoBancoHoras is not { } quitacao || _abrirCalculadora is null)
            return;
        var transferencia = CampoQuitacaoBancoHorasViewModel.Transferencia(quitacao);
        if (transferencia.Destino == destino)
            _abrirCalculadora(destino, transferencia.Valores);
    }

    private void AbrirRra()
    {
        if (_demonstrativo?.ParcelasRra is { Count: > 0 } parcelas && _abrirCalculadora is not null)
            _abrirCalculadora(TipoCalculadora.Rra, new Dictionary<string, string> { ["Parcelas do RRA"] = CampoParcelasRraViewModel.CriarImportacao(parcelas) });
    }

    // A rescisão recebe o FGTS devido de cada competência anterior ao desligamento e, se informada, a data de desligamento.
    private void LevarFgtsRescisao()
    {
        if (_demonstrativo?.DepositosFgts is not { Depositos.Count: > 0 } fgts || _abrirCalculadora is null)
            return;
        var valores = new Dictionary<string, string> { ["Depósitos históricos do FGTS"] = CampoDepositosFgtsViewModel.CriarImportacao(fgts.Depositos) };
        if (fgts.Desligamento is { } desligamento)
            valores["Data de desligamento"] = desligamento.ToString("dd/MM/yyyy", Cultura);
        _abrirCalculadora(TipoCalculadora.Rescisao, valores);
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
