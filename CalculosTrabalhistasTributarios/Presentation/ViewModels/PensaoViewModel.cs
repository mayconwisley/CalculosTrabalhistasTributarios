using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.Services;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Pensão alimentícia de um ou mais beneficiários e IRRF nas duas modalidades, com os rendimentos informados na própria janela.</summary>
public sealed class PensaoViewModel : ViewModelBase, ICalculoSalvavel, System.ComponentModel.IDataErrorInfo
{
    private const int MaximoBeneficiarios = 10;
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly ISimularPensaoUseCase _simulador;
    private readonly ITributacaoConsulta _tributacao;
    private readonly IUserNotifier _notificador;
    private readonly IRelatorioPdfService _relatorioPdf;
    private readonly IPlanilhaService _planilha;
    private readonly IArquivoDialogService _arquivoDialog;
    private readonly ContextoCompartilhado _contexto;
    private string _competencia;
    private string _valorBruto = "0,00";
    private string _baseInss = "0,00";
    private string _dependentes;
    private string _outrosDescontos = "0,00";
    private OpcaoCampo _ordemSelecionada;
    private SimulacaoPensaoDto? _ultimaSimulacao;
    private EntradaPensaoDto? _ultimaEntrada;
    private bool _ultimoCalculoDetalhado;
    private bool _resultadoDesatualizado;
    private bool _usarBrutoComoBaseInss = true;
    private bool _exibirMemoriaDetalhada;
    private long _versaoFormulario;
    private bool _calculando;
    private bool _temResultado;
    private bool _temMemoria;
    private string _explicacao = string.Empty;
    private string _mensagemMemoria = "Marque Exibir memória detalhada para conferir as iterações.";
    private IReadOnlyList<IndicadorResumoViewModel> _indicadoresResumo = [];
    private IReadOnlyList<PensaoPorBeneficiarioViewModel> _pensoesPorBeneficiario = [];
    private IReadOnlyList<ComparativoPensaoViewModel> _comparativoPensao = [];
    private IReadOnlyList<SecaoMemoriaPensaoViewModel> _memoriaPensao = [];
    private QuadroFaixasViewModel? _quadroInss;

    public PensaoViewModel(ISimularPensaoUseCase simulador, ITributacaoConsulta tributacao, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha, IArquivoDialogService arquivoDialog, ContextoCompartilhado contexto,
        IHistoricoDaJanelaFactory historico)
    {
        _simulador = simulador; _tributacao = tributacao; _notificador = notificador; _relatorioPdf = relatorioPdf; _planilha = planilha; _arquivoDialog = arquivoDialog; _contexto = contexto;
        _ordemSelecionada = OpcoesOrdem[0];
        Historico = historico.Criar(this);
        var anterior = contexto.Atual;
        _competencia = (anterior.Competencia ?? DateOnly.FromDateTime(DateTime.Today)).ToString("MM/yyyy", Cultura);
        _dependentes = (anterior.Dependentes ?? 0).ToString(Cultura);
        if (anterior.Salario is { } salario)
            ValorBruto = salario.ToString("N2", Cultura);
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(Competencia) or nameof(ValorBruto) or nameof(BaseInss)
                or nameof(Dependentes) or nameof(OutrosDescontos) or nameof(OrdemSelecionada))
            {
                InvalidarResultado();
                OnPropertyChanged("Item[]");
            }
        };
        SubirBeneficiarioCommand = new RelayCommand(item => MoverBeneficiario(item, -1), item => PodeMover(item, -1));
        DescerBeneficiarioCommand = new RelayCommand(item => MoverBeneficiario(item, 1), item => PodeMover(item, 1));
        var adicionar = new RelayCommand(_ => Beneficiarios.Add(NovoBeneficiario()), _ => Beneficiarios.Count < MaximoBeneficiarios);
        AdicionarBeneficiarioCommand = adicionar;
        RemoverBeneficiarioCommand = new RelayCommand(parametro =>
        {
            if (parametro is BeneficiarioPensaoViewModel beneficiario && Beneficiarios.Count > 1)
                Beneficiarios.Remove(beneficiario);
        }, item => item is BeneficiarioPensaoViewModel beneficiario && Beneficiarios.Contains(beneficiario) && Beneficiarios.Count > 1);
        Beneficiarios.Add(NovoBeneficiario());
        Beneficiarios.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TemVariosBeneficiarios));
            adicionar.RaiseCanExecuteChanged();
            ((RelayCommand)RemoverBeneficiarioCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SubirBeneficiarioCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DescerBeneficiarioCommand).RaiseCanExecuteChanged();
            InvalidarResultado();
        };
        CalcularCommand = new AsyncRelayCommand(() => CalcularAsync(ExibirMemoriaDetalhada), () => !_calculando);
        ExportarPdfCommand = new AsyncRelayCommand(ExportarPdfAsync, () => !ResultadoDesatualizado && _ultimaSimulacao is not null && _ultimaEntrada is not null);
        ExportarExcelCommand = new AsyncRelayCommand(
            () => ExportacaoPlanilha.SalvarAsync(_arquivoDialog, _notificador, $"pensao-alimenticia-{_ultimaEntrada!.Competencia:MM-yyyy}.xlsx",
                caminho => _planilha.GerarPensaoAsync(_ultimaSimulacao!, _ultimaEntrada!, caminho, CancellationToken.None)),
            () => !ResultadoDesatualizado && _ultimaSimulacao is not null && _ultimaEntrada is not null);
    }

    public string Competencia
    {
        get => _competencia;
        set
        {
            if (SetProperty(ref _competencia, value) && Beneficiarios.Any(beneficiario => beneficiario.EhSalarioMinimo))
                _ = AtualizarSalarioMinimoAsync();
        }
    }

    /// <summary>A base acompanha o bruto apenas enquanto a sincronização estiver habilitada.</summary>
    public string ValorBruto
    {
        get => _valorBruto;
        set
        {
            if (SetProperty(ref _valorBruto, value) && UsarBrutoComoBaseInss && TentarLerDecimal(value, out var valorBruto) && valorBruto >= 0m)
                BaseInss = valorBruto.ToString("N2", Cultura);
        }
    }
    public bool UsarBrutoComoBaseInss
    {
        get => _usarBrutoComoBaseInss;
        set
        {
            if (SetProperty(ref _usarBrutoComoBaseInss, value) && value)
                BaseInss = ValorBruto;
        }
    }
    public bool ResultadoDesatualizado { get => _resultadoDesatualizado; private set => SetProperty(ref _resultadoDesatualizado, value); }
    public bool ExibirMemoriaDetalhada
    {
        get => _exibirMemoriaDetalhada;
        set
        {
            if (SetProperty(ref _exibirMemoriaDetalhada, value) && !ResultadoDesatualizado && _ultimaSimulacao is { } simulacao && _ultimaEntrada is { } entrada)
            {
                _ultimoCalculoDetalhado = value;
                AtualizarApresentacao(simulacao, entrada, value);
            }
        }
    }
    public ICommand CalcularCommand { get; }
    public ICommand SubirBeneficiarioCommand { get; }
    public ICommand DescerBeneficiarioCommand { get; }

    private bool PodeMover(object? item, int deslocamento) => item is BeneficiarioPensaoViewModel beneficiario
        && Beneficiarios.IndexOf(beneficiario) is var indice && indice >= 0
        && indice + deslocamento >= 0 && indice + deslocamento < Beneficiarios.Count;

    private void MoverBeneficiario(object? item, int deslocamento)
    {
        if (PodeMover(item, deslocamento) && item is BeneficiarioPensaoViewModel beneficiario)
        {
            var indice = Beneficiarios.IndexOf(beneficiario);
            Beneficiarios.Move(indice, indice + deslocamento);
        }
    }

    private void InvalidarResultado()
    {
        _versaoFormulario++;
        if (_ultimaSimulacao is null)
            return;
        ResultadoDesatualizado = true;
        AtualizarExportacao();
    }

    private void AtualizarExportacao()
    {
        (ExportarPdfCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ExportarExcelCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    public string BaseInss { get => _baseInss; set => SetProperty(ref _baseInss, value); }
    public string Dependentes { get => _dependentes; set => SetProperty(ref _dependentes, value); }
    public string OutrosDescontos { get => _outrosDescontos; set => SetProperty(ref _outrosDescontos, value); }

    /// <summary>Uma linha por beneficiário, na ordem da decisão.</summary>
    public ObservableCollection<BeneficiarioPensaoViewModel> Beneficiarios { get; } = [];
    public bool TemVariosBeneficiarios => Beneficiarios.Count > 1;

    /// <summary>Com mais de um beneficiário: todas as pensões sobre a mesma base, ou cada uma depois de descontar as anteriores.</summary>
    public IReadOnlyList<OpcaoCampo> OpcoesOrdem { get; } =
    [
        new("Todas sobre a mesma base", false),
        new("Cada uma após descontar as anteriores", true)
    ];
    public OpcaoCampo OrdemSelecionada { get => _ordemSelecionada; set => SetProperty(ref _ordemSelecionada, value); }

    public bool TemResultado { get => _temResultado; private set => SetProperty(ref _temResultado, value); }
    public bool TemMemoria { get => _temMemoria; private set => SetProperty(ref _temMemoria, value); }

    /// <summary>Como a pensão foi obtida e para que servem o valor bruto, o INSS e o IRRF de quem paga.</summary>
    public string Explicacao { get => _explicacao; private set => SetProperty(ref _explicacao, value); }
    public string MensagemMemoria { get => _mensagemMemoria; private set => SetProperty(ref _mensagemMemoria, value); }
    public IReadOnlyList<IndicadorResumoViewModel> IndicadoresResumo { get => _indicadoresResumo; private set => SetProperty(ref _indicadoresResumo, value); }
    public IReadOnlyList<PensaoPorBeneficiarioViewModel> PensoesPorBeneficiario { get => _pensoesPorBeneficiario; private set { SetProperty(ref _pensoesPorBeneficiario, value); OnPropertyChanged(nameof(TemPensoesPorBeneficiario)); } }
    public bool TemPensoesPorBeneficiario => PensoesPorBeneficiario.Count > 1;
    public IReadOnlyList<ComparativoPensaoViewModel> ComparativoPensao { get => _comparativoPensao; private set => SetProperty(ref _comparativoPensao, value); }
    public IReadOnlyList<SecaoMemoriaPensaoViewModel> MemoriaPensao { get => _memoriaPensao; private set => SetProperty(ref _memoriaPensao, value); }

    /// <summary>Faixas do INSS em tabela na memória detalhada; nulo sem o detalhe, sem base de INSS ou antes de 03/2020.</summary>
    public QuadroFaixasViewModel? QuadroInss { get => _quadroInss; private set { SetProperty(ref _quadroInss, value); OnPropertyChanged(nameof(TemQuadroInss)); } }
    public bool TemQuadroInss => QuadroInss is not null;
    public ICommand AdicionarBeneficiarioCommand { get; }
    public ICommand RemoverBeneficiarioCommand { get; }
    public ICommand ExportarPdfCommand { get; }
    public ICommand ExportarExcelCommand { get; }
    public HistoricoDaJanela Historico { get; }

    public string TipoHistorico => "Pensao";
    public string NomeCalculadora => "Pensão alimentícia";
    public string NomeSugerido => $"Pensão alimentícia - {Competencia}";

    public DadosFormulario ExportarDados() => new(
        new()
        {
            [nameof(Competencia)] = Competencia,
            [nameof(ValorBruto)] = ValorBruto,
            [nameof(BaseInss)] = BaseInss,
            [nameof(Dependentes)] = Dependentes,
            [nameof(OutrosDescontos)] = OutrosDescontos,
            [nameof(OrdemSelecionada)] = OrdemSelecionada.Texto,
            [nameof(UsarBrutoComoBaseInss)] = UsarBrutoComoBaseInss ? "Sim" : "Não",
            ["Detalhado"] = ExibirMemoriaDetalhada ? "Sim" : "Não"
        },
        Beneficiarios.Select(beneficiario => new Dictionary<string, string>
        {
            [nameof(BeneficiarioPensaoViewModel.Nome)] = beneficiario.Nome,
            [nameof(BeneficiarioPensaoViewModel.BaseSelecionada)] = beneficiario.BaseSelecionada.Texto,
            [nameof(BeneficiarioPensaoViewModel.Percentual)] = beneficiario.Percentual,
            [nameof(BeneficiarioPensaoViewModel.ValorPensao)] = beneficiario.ValorPensao
        }).ToList());

    public void ImportarDados(DadosFormulario dados)
    {
        var resultado = TentarImportarDados(dados);
        if (resultado.Falhou)
            _notificador.MostrarFalha(resultado.Erro, "Não foi possível importar a pensão. O formulário atual foi preservado.");
    }

    public Result TentarImportarDados(DadosFormulario dados)
    {
        var preparacao = PrepararImportacao(dados, out var beneficiarios, out var ordem);
        if (preparacao.Falhou)
            return preparacao;

        UsarBrutoComoBaseInss = false;
        Competencia = dados.Valor(nameof(Competencia));
        ValorBruto = dados.Valor(nameof(ValorBruto));
        BaseInss = dados.Valor(nameof(BaseInss));
        Dependentes = dados.Valor(nameof(Dependentes));
        OutrosDescontos = dados.Valor(nameof(OutrosDescontos));
        OrdemSelecionada = ordem;
        ExibirMemoriaDetalhada = dados.Valor("Detalhado") == "Sim";
        UsarBrutoComoBaseInss = dados.Valor(nameof(UsarBrutoComoBaseInss),
            TentarLerDecimal(BaseInss, out var baseSalva) && TentarLerDecimal(ValorBruto, out var brutoSalvo) && baseSalva == brutoSalvo ? "Sim" : "Não") == "Sim";
        Beneficiarios.Clear();
        foreach (var beneficiario in beneficiarios)
            Beneficiarios.Add(beneficiario);
        if (beneficiarios.Any(item => item.EhSalarioMinimo))
            _ = AtualizarSalarioMinimoAsync();
        return Result.Ok();
    }

    private Result PrepararImportacao(DadosFormulario dados,
        out List<BeneficiarioPensaoViewModel> beneficiarios, out OpcaoCampo ordem)
    {
        beneficiarios = [];
        ordem = OpcoesOrdem[0];
        var campos = new[] { nameof(Competencia), nameof(ValorBruto), nameof(BaseInss), nameof(Dependentes), nameof(OutrosDescontos) };
        if (dados.Campos is null || campos.Any(campo => !dados.Campos.TryGetValue(campo, out var texto) || texto is null)
            || dados.Linhas is not { Count: >= 1 and <= MaximoBeneficiarios })
            return Erro.Validacao("Informe os campos dos rendimentos e de 1 a 10 beneficiários no histórico.");

        var textoOrdem = dados.Valor(nameof(OrdemSelecionada), OpcoesOrdem[0].Texto);
        var opcaoOrdem = OpcoesOrdem.FirstOrDefault(item => item.Texto == textoOrdem);
        if (opcaoOrdem is null)
            return Erro.Validacao("A ordem de cálculo salva não é reconhecida.");
        ordem = opcaoOrdem;
        foreach (var campo in new[] { "Detalhado", nameof(UsarBrutoComoBaseInss) })
            if (dados.Campos.TryGetValue(campo, out var texto) && texto is not ("Sim" or "Não"))
                return Erro.Validacao($"A opção {campo} deve ser Sim ou Não.");

        foreach (var linha in dados.Linhas)
        {
            if (linha is null)
                return Erro.Validacao("Existe um beneficiário sem dados.");
            var beneficiario = NovoBeneficiario();
            var basePensao = beneficiario.BasesPensao.FirstOrDefault(item => item.Texto == linha.GetValueOrDefault(nameof(BeneficiarioPensaoViewModel.BaseSelecionada)));
            if (basePensao is null)
                return Erro.Validacao($"A base da pensão do beneficiário {beneficiarios.Count + 1} não é reconhecida.");
            var campoValor = (BasePensao)basePensao.Valor == BasePensao.ValorFixo
                ? nameof(BeneficiarioPensaoViewModel.ValorPensao) : nameof(BeneficiarioPensaoViewModel.Percentual);
            if (!linha.TryGetValue(campoValor, out var valor) || valor is null)
                return Erro.Validacao($"Falta o campo {campoValor} do beneficiário {beneficiarios.Count + 1}.");
            beneficiario.Nome = linha.GetValueOrDefault(nameof(BeneficiarioPensaoViewModel.Nome)) ?? $"Beneficiário {beneficiarios.Count + 1}";
            beneficiario.Percentual = linha.GetValueOrDefault(nameof(BeneficiarioPensaoViewModel.Percentual)) ?? "0,00";
            beneficiario.ValorPensao = linha.GetValueOrDefault(nameof(BeneficiarioPensaoViewModel.ValorPensao)) ?? "0,00";
            // A consulta do salário mínimo só começa depois da importação completa.
            beneficiario.DefinirBaseSemConsulta(basePensao);
            beneficiarios.Add(beneficiario);
        }
        return Result.Ok();
    }

    public Task RecalcularAsync() => CalcularAsync(ExibirMemoriaDetalhada);

    private BeneficiarioPensaoViewModel NovoBeneficiario()
    {
        var beneficiario = new BeneficiarioPensaoViewModel($"Beneficiário {Beneficiarios.Count + 1}", () => _ = AtualizarSalarioMinimoAsync());
        beneficiario.PropertyChanged += (_, args) =>
        {
            if (Beneficiarios.Contains(beneficiario) && (args.PropertyName is nameof(BeneficiarioPensaoViewModel.Nome)
                or nameof(BeneficiarioPensaoViewModel.BaseSelecionada) or nameof(BeneficiarioPensaoViewModel.Percentual)
                or nameof(BeneficiarioPensaoViewModel.ValorPensao)))
                InvalidarResultado();
        };
        return beneficiario;
    }

    private async Task CalcularAsync(bool detalhar)
    {
        if (_calculando || !TentarLerEntrada(out var entrada))
            return;

        var versao = _versaoFormulario;
        _calculando = true;
        try
        {
            var simulacao = await _simulador.ExecutarAsync(new SimularPensaoRequest(entrada.Competencia, entrada.ValorBruto, entrada.BaseInss, entrada.Dependentes, entrada.Beneficiarios, entrada.OutrosDescontos, entrada.Sucessiva), CancellationToken.None);
            if (simulacao.Falhou)
            {
                _notificador.MostrarFalha(simulacao.Erro, "Não foi possível calcular a pensão.");
                return;
            }
            if (versao != _versaoFormulario)
            {
                _notificador.MostrarAviso("Os dados foram alterados durante o cálculo. Calcule novamente.");
                return;
            }
            ResultadoDesatualizado = false;
            ExibirMemoriaDetalhada = detalhar;
            var resultado = simulacao.Valor;
            _ultimaSimulacao = resultado;
            _ultimaEntrada = entrada;
            _ultimoCalculoDetalhado = detalhar;
            _contexto.Registrar(new ContextoCalculo(entrada.Competencia, entrada.ValorBruto > 0m ? entrada.ValorBruto : null, entrada.Dependentes));
            AtualizarApresentacao(resultado, entrada, detalhar);
            ((AsyncRelayCommand)ExportarPdfCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)ExportarExcelCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex) { _notificador.MostrarErro("Não foi possível calcular a pensão.", ex); }
        finally { _calculando = false; }
    }

    public string Error => string.Empty;
    public string this[string campo] => campo switch
    {
        nameof(Competencia) => DateTime.TryParseExact(Competencia.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out _) ? "" : "Informe MM/AAAA.",
        nameof(Dependentes) => int.TryParse(Dependentes, out var quantidade) && quantidade is >= 0 and <= 100 ? "" : "Informe de 0 a 100 dependentes.",
        nameof(ValorBruto) => ValidarMoeda(ValorBruto),
        nameof(BaseInss) => ValidarMoeda(BaseInss),
        nameof(OutrosDescontos) => ValidarMoeda(OutrosDescontos) is { Length: > 0 } erro ? erro
            : TentarLerDecimal(OutrosDescontos, out var desconto) && TentarLerDecimal(ValorBruto, out var bruto) && desconto > bruto
                ? "Não pode superar o valor bruto." : "",
        _ => ""
    };

    private static string ValidarMoeda(string texto) => TentarLerDecimal(texto, out var valor) && LimitesPensao.MoedaValida(valor)
        ? "" : "Informe de 0 a 999.999.999,99, com até duas casas decimais.";

    private bool TentarLerEntrada(out EntradaPensaoDto entrada)
    {
        entrada = default!;
        (string Campo, string Rotulo)[] campos =
        [
            (nameof(Competencia), "Competência"), (nameof(ValorBruto), "Valor bruto"),
            (nameof(BaseInss), "Base de INSS"), (nameof(Dependentes), "Dependentes"),
            (nameof(OutrosDescontos), "Abatimentos dos rendimentos")
        ];
        foreach (var (campo, rotulo) in campos)
            if (this[campo] is { Length: > 0 } erro)
            {
                _notificador.MostrarAviso($"{rotulo}: {erro}");
                return false;
            }

        var competencia = DateOnly.ParseExact(Competencia.Trim(), "MM/yyyy", Cultura);
        TentarLerDecimal(ValorBruto, out var valorBruto);
        TentarLerDecimal(BaseInss, out var baseInss);
        TentarLerDecimal(OutrosDescontos, out var descontos);
        var dependentes = int.Parse(Dependentes);
        var beneficiarios = new List<BeneficiarioPensao>(Beneficiarios.Count);
        foreach (var (beneficiario, indice) in Beneficiarios.Select((beneficiario, indice) => (beneficiario, indice)))
        {
            var nome = string.IsNullOrWhiteSpace(beneficiario.Nome) ? $"Beneficiário {indice + 1}" : beneficiario.Nome.Trim();
            var percentual = 0m;
            var valorPensao = 0m;
            if (beneficiario.EhPercentual)
                TentarLerDecimal(beneficiario.Percentual, out percentual);
            else
                TentarLerDecimal(beneficiario.ValorPensao, out valorPensao);
            if (beneficiario[beneficiario.EhPercentual ? nameof(beneficiario.Percentual) : nameof(beneficiario.ValorPensao)] is { Length: > 0 } erroBeneficiario)
            {
                _notificador.MostrarAviso($"{nome}: {erroBeneficiario}");
                return false;
            }
            var regra = beneficiario.EhPercentual ? new RegraPensao(beneficiario.Base, percentual, 0m) : new RegraPensao(BasePensao.ValorFixo, 0m, valorPensao);
            beneficiarios.Add(new BeneficiarioPensao(nome, regra));
        }

        entrada = new EntradaPensaoDto(competencia, valorBruto, baseInss, dependentes, beneficiarios, descontos, beneficiarios.Count > 1 && (bool)OrdemSelecionada.Valor);
        return true;
    }

    private static bool TentarLerDecimal(string valor, out decimal resultado) => LeituraNumerica.TentarLer(valor, out resultado);

    /// <summary>Preenche o salário mínimo da competência nos beneficiários com a base "% do salário mínimo".</summary>
    private async Task AtualizarSalarioMinimoAsync()
    {
        var texto = Competencia.Trim();
        string rotulo, valor;
        if (!DateTime.TryParseExact(texto, "MM/yyyy", Cultura, DateTimeStyles.None, out var data))
        {
            rotulo = "Salário mínimo";
            valor = "Informe a competência";
        }
        else
        {
            try
            {
                var salarioMinimo = await _tributacao.ObterSalarioMinimoAsync(DateOnly.FromDateTime(data), CancellationToken.None);
                valor = salarioMinimo is { } salario ? salario.ToString("N2", Cultura) : "Não cadastrado";
            }
            catch (Exception)
            {
                // Sem tabelas para a competência; o motivo aparece ao calcular.
                valor = "Não cadastrado";
            }
            // Se a competência mudou durante a consulta, uma consulta mais nova vai preencher o campo.
            if (Competencia.Trim() != texto)
                return;
            rotulo = $"Salário mínimo de {texto}";
        }

        foreach (var beneficiario in Beneficiarios.Where(beneficiario => beneficiario.EhSalarioMinimo))
        {
            beneficiario.RotuloSalarioMinimo = rotulo;
            beneficiario.SalarioMinimo = valor;
        }
    }

    private void AtualizarApresentacao(SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada, bool detalhar)
    {
        // Os cartões seguem a modalidade de menor IRRF, a que a fonte pagadora aplica; o comparativo mostra as duas.
        var aplicada = simulacao.Aplicada;
        var pensoes = aplicada.Detalhes[^1].Beneficiarios;
        var temRendimentos = entrada.ValorBruto > 0m;
        var liquido = entrada.ValorBruto - entrada.OutrosDescontos - simulacao.ValorInss - aplicada.Imposto - aplicada.Pensao;
        IndicadoresResumo =
        [
            new(pensoes.Count > 1 ? "Pensões" : "Pensão", Moeda(aplicada.Pensao), pensoes.Count > 1 ? $"Soma das {pensoes.Count} pensões" : pensoes[0].Descrever(Moeda, FormatarPercentual)),
            new("Líquido de quem paga", temRendimentos ? Moeda(liquido) : "—", temRendimentos
                ? $"Bruto menos {(entrada.OutrosDescontos > 0m ? "abatimentos, " : "")}INSS, IRRF e {(pensoes.Count > 1 ? "pensões" : "pensão")}"
                : "Sem valor bruto informado"),
            new(pensoes.Count > 1 ? "IRRF com pensões" : "IRRF com pensão", Moeda(aplicada.Imposto),
                $"{(aplicada.DeduzPensao ? "Deduções legais, com a pensão" : "Desconto simplificado")}; {MemoriaCalculoPensao.FaixaIrrf(aplicada.Detalhes[^1].Aliquota, aplicada.Imposto)}"),
            new(pensoes.Count > 1 ? "IRRF sem pensões" : "IRRF sem pensão", Moeda(simulacao.ImpostoSemPensao),
                $"Se não houvesse a pensão; {MemoriaCalculoPensao.FaixaIrrf(simulacao.AliquotaIrrfSemPensao, simulacao.ImpostoSemPensao)}"),
            new("Economia de IRRF", Moeda(simulacao.EconomiaIrrf), simulacao.EconomiaIrrf > 0m ? "Pela dedução da pensão" : "A pensão não reduziu o IRRF"),
            new("INSS", Moeda(simulacao.ValorInss), DescreverBaseInss(simulacao, entrada.BaseInss))
        ];
        PensoesPorBeneficiario = pensoes.Select(pensao => new PensaoPorBeneficiarioViewModel(pensao.Nome, pensao.Descrever(Moeda, FormatarPercentual), Moeda(pensao.Pensao))).ToArray();
        Explicacao = MemoriaCalculoPensao.Explicacao(simulacao, entrada, Moeda, FormatarPercentual);
        ComparativoPensao = simulacao.Modalidades.Select(modalidade => CriarComparativo(modalidade) with { Modalidade = modalidade.Nome + (ReferenceEquals(modalidade, aplicada) ? " (aplicada)" : "") }).ToArray();
        MemoriaPensao = detalhar
            ? simulacao.Modalidades.Select(modalidade => CriarMemoria(modalidade, entrada, simulacao.ValorInss)).ToArray()
            : [];
        QuadroInss = detalhar && simulacao.QuadroInss is { } quadroInss ? QuadroFaixasViewModel.De(quadroInss) : null;
        TemResultado = true;
        TemMemoria = detalhar;
        MensagemMemoria = detalhar ? string.Empty : "Marque Exibir memória detalhada para conferir as iterações.";
    }

    private static string DescreverBaseInss(SimulacaoPensaoDto simulacao, decimal baseInformada)
    {
        // As parcelas da apuração já refletem o teto da competência, inclusive no regime anterior a 03/2020.
        var baseUtilizada = simulacao.FaixasInss.Sum(faixa => faixa.BaseCalculada);
        var descricao = $"Base utilizada: {Moeda(baseUtilizada)}";
        if (baseInformada > baseUtilizada)
            descricao += $" (limitada ao teto); base informada: {Moeda(baseInformada)}";
        if (simulacao.FaixasInss.Count > 0)
            descricao += $"; {MemoriaCalculoPensao.FaixaInss(simulacao)}";
        return descricao;
    }

    private static ComparativoPensaoViewModel CriarComparativo(ModalidadePensaoDto modalidade) =>
        new(modalidade.Nome, PrimeiraMaiuscula(MemoriaCalculoPensao.FaixaIrrf(modalidade.Detalhes[^1].Aliquota, modalidade.Imposto)), Moeda(modalidade.Imposto), Moeda(modalidade.Pensao), Moeda(modalidade.Total));

    private static string PrimeiraMaiuscula(string texto) => texto.Length == 0 ? texto : char.ToUpper(texto[0], Cultura) + texto[1..];

    private static SecaoMemoriaPensaoViewModel CriarMemoria(ModalidadePensaoDto modalidade, EntradaPensaoDto entrada, decimal valorInss)
    {
        var iteracoes = modalidade.Detalhes
            .Select(detalhe => new IteracaoMemoriaPensaoViewModel($"Iteração {detalhe.Sequencia}",
                MemoriaCalculoPensao.Iteracao(modalidade, detalhe, entrada, valorInss, Moeda, FormatarPercentual).Select(formula => new FormulaCalculoViewModel(formula.Titulo, formula.Formula)).ToArray()))
            .ToArray();

        return new SecaoMemoriaPensaoViewModel($"{modalidade.Nome} - memória de cálculo", $"Iterações: {modalidade.Iteracoes} | Total: {Moeda(modalidade.Total)}", iteracoes,
            modalidade.QuadroIrrf is { } quadro ? QuadroFaixasViewModel.De(quadro) : null, MemoriaCalculoPensao.ConferenciaDasFaixas(modalidade, Moeda));
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", Cultura);
    private static string FormatarPercentual(decimal valor) => valor.ToString("N2", Cultura) + "%";

    private async Task ExportarPdfAsync()
    {
        if (ResultadoDesatualizado || _ultimaSimulacao is null || _ultimaEntrada is null)
            return;

        var caminhoArquivo = _arquivoDialog.SolicitarDestinoPdf($"relatorio-pensao-alimenticia-{_ultimaEntrada.Competencia:MM-yyyy}.pdf");
        if (caminhoArquivo is null)
            return;

        try
        {
            await _relatorioPdf.GerarRelatorioPensaoAsync(_ultimaSimulacao, _ultimaEntrada, _ultimoCalculoDetalhado, caminhoArquivo, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro("Não foi possível gerar o relatório em PDF.", exception);
        }
    }
}
