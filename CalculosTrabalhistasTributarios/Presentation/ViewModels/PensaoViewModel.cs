using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.Services;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Pensão alimentícia de um ou mais beneficiários e IRRF nas duas modalidades, com os rendimentos informados na própria janela.</summary>
public sealed class PensaoViewModel : ViewModelBase, ICalculoSalvavel
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
    private bool _detalharAoReabrir;
    private bool _temResultado;
    private bool _temMemoria;
    private string _explicacao = string.Empty;
    private string _mensagemMemoria = "Selecione Detalhar para exibir a memória de cálculo por iteração.";
    private IReadOnlyList<IndicadorResumoViewModel> _indicadoresResumo = [];
    private IReadOnlyList<PensaoPorBeneficiarioViewModel> _pensoesPorBeneficiario = [];
    private IReadOnlyList<ComparativoPensaoViewModel> _comparativoPensao = [];
    private IReadOnlyList<SecaoMemoriaPensaoViewModel> _memoriaPensao = [];

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
        var adicionar = new RelayCommand(_ => Beneficiarios.Add(NovoBeneficiario()), _ => Beneficiarios.Count < MaximoBeneficiarios);
        AdicionarBeneficiarioCommand = adicionar;
        RemoverBeneficiarioCommand = new RelayCommand(parametro =>
        {
            if (parametro is BeneficiarioPensaoViewModel beneficiario && Beneficiarios.Count > 1)
                Beneficiarios.Remove(beneficiario);
        });
        Beneficiarios.Add(NovoBeneficiario());
        Beneficiarios.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TemVariosBeneficiarios));
            adicionar.RaiseCanExecuteChanged();
        };
        CalcularResumoCommand = new AsyncRelayCommand(() => CalcularAsync(false));
        CalcularDetalheCommand = new AsyncRelayCommand(() => CalcularAsync(true));
        ExportarPdfCommand = new AsyncRelayCommand(ExportarPdfAsync, () => _ultimaSimulacao is not null && _ultimaEntrada is not null);
        ExportarExcelCommand = new AsyncRelayCommand(
            () => ExportacaoPlanilha.SalvarAsync(_arquivoDialog, _notificador, $"pensao-alimenticia-{_ultimaEntrada!.Competencia:MM-yyyy}.xlsx",
                caminho => _planilha.GerarPensaoAsync(_ultimaSimulacao!, _ultimaEntrada!, caminho, CancellationToken.None)),
            () => _ultimaSimulacao is not null && _ultimaEntrada is not null);
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

    /// <summary>Ao alterar o valor bruto, a base de INSS acompanha; ela só precisa ser editada quando for diferente.</summary>
    public string ValorBruto
    {
        get => _valorBruto;
        set
        {
            if (SetProperty(ref _valorBruto, value) && TentarLerDecimal(value, out var valorBruto) && valorBruto >= 0m)
                BaseInss = valorBruto.ToString("N2", Cultura);
        }
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
    public ICommand AdicionarBeneficiarioCommand { get; }
    public ICommand RemoverBeneficiarioCommand { get; }
    public ICommand CalcularResumoCommand { get; }
    public ICommand CalcularDetalheCommand { get; }
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
            ["Detalhado"] = _ultimoCalculoDetalhado ? "Sim" : "Não"
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
        Competencia = dados.Valor(nameof(Competencia), Competencia);
        // O valor bruto preenche a base de INSS; a base salva vem depois, para valer a que foi informada.
        ValorBruto = dados.Valor(nameof(ValorBruto), ValorBruto);
        BaseInss = dados.Valor(nameof(BaseInss), BaseInss);
        Dependentes = dados.Valor(nameof(Dependentes), Dependentes);
        OutrosDescontos = dados.Valor(nameof(OutrosDescontos), OutrosDescontos);
        OrdemSelecionada = OpcoesOrdem.FirstOrDefault(opcao => opcao.Texto == dados.Valor(nameof(OrdemSelecionada))) ?? OrdemSelecionada;
        _detalharAoReabrir = dados.Valor("Detalhado") == "Sim";
        if (dados.Linhas is not { Count: > 0 } linhas)
            return;

        Beneficiarios.Clear();
        foreach (var linha in linhas.Take(MaximoBeneficiarios))
        {
            var beneficiario = NovoBeneficiario();
            beneficiario.Nome = linha.GetValueOrDefault(nameof(BeneficiarioPensaoViewModel.Nome), beneficiario.Nome);
            if (beneficiario.BasesPensao.FirstOrDefault(opcao => opcao.Texto == linha.GetValueOrDefault(nameof(BeneficiarioPensaoViewModel.BaseSelecionada))) is { } basePensao)
                beneficiario.BaseSelecionada = basePensao;
            beneficiario.Percentual = linha.GetValueOrDefault(nameof(BeneficiarioPensaoViewModel.Percentual), beneficiario.Percentual);
            beneficiario.ValorPensao = linha.GetValueOrDefault(nameof(BeneficiarioPensaoViewModel.ValorPensao), beneficiario.ValorPensao);
            Beneficiarios.Add(beneficiario);
        }
    }

    public Task RecalcularAsync() => CalcularAsync(_detalharAoReabrir);

    private BeneficiarioPensaoViewModel NovoBeneficiario() =>
        new($"Beneficiário {Beneficiarios.Count + 1}", () => _ = AtualizarSalarioMinimoAsync());

    private async Task CalcularAsync(bool detalhar)
    {
        if (!TentarLerEntrada(out var entrada))
            return;

        try
        {
            var simulacao = await _simulador.ExecutarAsync(new SimularPensaoRequest(entrada.Competencia, entrada.ValorBruto, entrada.BaseInss, entrada.Dependentes, entrada.Beneficiarios, entrada.OutrosDescontos, entrada.Sucessiva), CancellationToken.None);
            if (simulacao.Falhou)
            {
                _notificador.MostrarFalha(simulacao.Erro, "Não foi possível calcular a pensão.");
                return;
            }
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
    }

    private bool TentarLerEntrada(out EntradaPensaoDto entrada)
    {
        entrada = default!;
        if (!DateTime.TryParseExact(Competencia.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var competencia)
            || !TentarLerDecimal(ValorBruto, out var valorBruto) || !TentarLerDecimal(BaseInss, out var baseInss) || !int.TryParse(Dependentes, out var dependentes)
            || !TentarLerDecimal(OutrosDescontos, out var descontos)
            || valorBruto < 0m || baseInss < 0m || dependentes < 0 || descontos < 0m)
        {
            _notificador.MostrarAviso("Informe uma competência válida (MM/AAAA), valores monetários válidos e dependentes maior ou igual a zero.");
            return false;
        }
        if (descontos > valorBruto)
        {
            _notificador.MostrarAviso("Outros descontos não podem ser maiores que o valor bruto.");
            return false;
        }

        var beneficiarios = new List<BeneficiarioPensao>(Beneficiarios.Count);
        foreach (var (beneficiario, indice) in Beneficiarios.Select((beneficiario, indice) => (beneficiario, indice)))
        {
            var nome = string.IsNullOrWhiteSpace(beneficiario.Nome) ? $"Beneficiário {indice + 1}" : beneficiario.Nome.Trim();
            // Com um só beneficiário, as mensagens não precisam dizer de quem é o campo.
            var quem = Beneficiarios.Count > 1 ? $" de {nome}" : "";
            if (!TentarLerDecimal(beneficiario.Percentual, out var percentual) || !TentarLerDecimal(beneficiario.ValorPensao, out var valorPensao) || valorPensao < 0m)
            {
                _notificador.MostrarAviso($"Informe o percentual e o valor da pensão{quem} em formato válido.");
                return false;
            }
            var regra = beneficiario.EhPercentual ? new RegraPensao(beneficiario.Base, percentual, 0m) : new RegraPensao(BasePensao.ValorFixo, 0m, valorPensao);
            if (regra.EhPercentual && (percentual < 0m || percentual > regra.PercentualMaximo))
            {
                _notificador.MostrarAviso(regra.Base == BasePensao.SalarioMinimo
                    ? $"O percentual do salário mínimo{quem} deve estar entre 0 e 1.000 (150 para 1,5 salário mínimo)."
                    : $"O percentual da pensão{quem} deve estar entre 0 e 100.");
                return false;
            }
            beneficiarios.Add(new BeneficiarioPensao(nome, regra));
        }

        entrada = new EntradaPensaoDto(DateOnly.FromDateTime(competencia), valorBruto, baseInss, dependentes, beneficiarios, descontos, beneficiarios.Count > 1 && (bool)OrdemSelecionada.Valor);
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
                ? $"Bruto menos {(entrada.OutrosDescontos > 0m ? "outros descontos, " : "")}INSS, IRRF e {(pensoes.Count > 1 ? "pensões" : "pensão")}"
                : "Sem valor bruto informado"),
            new(pensoes.Count > 1 ? "IRRF com pensões" : "IRRF com pensão", Moeda(aplicada.Imposto), aplicada.DeduzPensao ? "Deduções legais, com a pensão" : "Desconto simplificado"),
            new(pensoes.Count > 1 ? "IRRF sem pensões" : "IRRF sem pensão", Moeda(simulacao.ImpostoSemPensao), "Se não houvesse a pensão"),
            new("Economia de IRRF", Moeda(simulacao.EconomiaIrrf), simulacao.EconomiaIrrf > 0m ? "Pela dedução da pensão" : "A pensão não reduziu o IRRF"),
            new("INSS", Moeda(simulacao.ValorInss), $"Base: {Moeda(entrada.BaseInss)}")
        ];
        PensoesPorBeneficiario = pensoes.Select(pensao => new PensaoPorBeneficiarioViewModel(pensao.Nome, pensao.Descrever(Moeda, FormatarPercentual), Moeda(pensao.Pensao))).ToArray();
        Explicacao = MemoriaCalculoPensao.Explicacao(simulacao, entrada, Moeda, FormatarPercentual);
        ComparativoPensao = simulacao.Modalidades.Select(CriarComparativo).ToArray();
        MemoriaPensao = detalhar
            ? simulacao.Modalidades.Select(modalidade => CriarMemoria(modalidade, entrada, simulacao.ValorInss)).ToArray()
            : [];
        TemResultado = true;
        TemMemoria = detalhar;
        MensagemMemoria = detalhar ? string.Empty : "Selecione Detalhar para exibir a memória de cálculo por iteração.";
    }

    private static ComparativoPensaoViewModel CriarComparativo(ModalidadePensaoDto modalidade) =>
        new(modalidade.Nome, Moeda(modalidade.Imposto), Moeda(modalidade.Pensao), Moeda(modalidade.Total));

    private static SecaoMemoriaPensaoViewModel CriarMemoria(ModalidadePensaoDto modalidade, EntradaPensaoDto entrada, decimal valorInss)
    {
        var iteracoes = modalidade.Detalhes
            .Select(detalhe => new IteracaoMemoriaPensaoViewModel($"Iteração {detalhe.Sequencia}",
                MemoriaCalculoPensao.Iteracao(modalidade, detalhe, entrada, valorInss, Moeda, FormatarPercentual).Select(formula => new FormulaCalculoViewModel(formula.Titulo, formula.Formula)).ToArray()))
            .ToArray();

        return new SecaoMemoriaPensaoViewModel($"{modalidade.Nome} - memória de cálculo", $"Iterações: {modalidade.Iteracoes} | Total: {Moeda(modalidade.Total)}", iteracoes);
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", Cultura);
    private static string FormatarPercentual(decimal valor) => valor.ToString("N2", Cultura) + "%";

    private async Task ExportarPdfAsync()
    {
        if (_ultimaSimulacao is null || _ultimaEntrada is null)
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
