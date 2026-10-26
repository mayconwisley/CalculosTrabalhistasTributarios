using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;

public sealed partial class SaqueAniversarioViewModel : ObservableObject, IFormularioEditavelMobile
{
    private readonly ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest> _simular;
    private readonly ILogger<SaqueAniversarioViewModel> _logger;
    private readonly ICompartilhamentoPdf _compartilhamento;

    public SaqueAniversarioViewModel(ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest> simular,
        ILogger<SaqueAniversarioViewModel> logger, ICompartilhamentoPdf compartilhamento)
    {
        _simular = simular;
        _logger = logger;
        _compartilhamento = compartilhamento;
    }

    public IReadOnlyList<string> Meses { get; } = CultureInfo.GetCultureInfo("pt-BR")
        .DateTimeFormat.MonthNames.Take(12).ToArray();
    public event EventHandler? FormularioAlterado;
    public string Titulo => "Saque-aniversário do FGTS";
    public long? HistoricoId { get; set; }

    public DadosFormularioMobile ExportarDados() => new(1, new Dictionary<string, ValorCampoMobile>
    {
        [nameof(Saldo)] = new(Saldo, 0, false), [nameof(GarantiaBloqueada)] = new(GarantiaBloqueada, 0, false),
        [nameof(MultaIncluida)] = new(MultaIncluida, 0, false), [nameof(ParcelaComprometida)] = new(ParcelaComprometida, 0, false),
        [nameof(SaldoIncluiGarantia)] = new("", 0, SaldoIncluiGarantia), [nameof(IndiceMes)] = new("", IndiceMes, false),
        [nameof(DataAnalise)] = new(DataAnalise, 0, false), [nameof(IndiceCarencia)] = new("", IndiceCarencia, false),
        [nameof(IndiceContratoNovo)] = new("", IndiceContratoNovo, false), [nameof(IndiceProximoCedido)] = new("", IndiceProximoCedido, false)
    }, new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, ValorCampoMobile>>>());

    public Result TentarImportarDados(DadosFormularioMobile dados)
    {
        var campos = dados.Campos;
        if (dados.Versao != 1 || campos is null || dados.Grades is null || dados.Grades.Count != 0
            || !campos.TryGetValue(nameof(Saldo), out var saldo) || !campos.TryGetValue(nameof(GarantiaBloqueada), out var garantia)
            || !campos.TryGetValue(nameof(MultaIncluida), out var multa) || !campos.TryGetValue(nameof(ParcelaComprometida), out var parcela)
            || !campos.TryGetValue(nameof(SaldoIncluiGarantia), out var inclui) || !campos.TryGetValue(nameof(IndiceMes), out var mes)
            || !new[] { saldo, garantia, multa, parcela }.All(valor => valor is not null && valor.Valor is { Length: <= 40 })
            || inclui is null || mes is null || mes.Indice is < -1 or > 11
            || (campos.TryGetValue(nameof(DataAnalise), out var data) && (data?.Valor is not { Length: <= 20 }))
            || !IndiceValido(campos, nameof(IndiceCarencia)) || !IndiceValido(campos, nameof(IndiceContratoNovo))
            || !IndiceValido(campos, nameof(IndiceProximoCedido)))
            return Erro.Validacao("Formulário salvo: confira os valores e o mês de aniversário.");
        Saldo = saldo.Valor; GarantiaBloqueada = garantia.Valor; MultaIncluida = multa.Valor;
        ParcelaComprometida = parcela.Valor; SaldoIncluiGarantia = inclui.Marcado; IndiceMes = mes.Indice;
        if (campos.TryGetValue(nameof(DataAnalise), out var dataSalva)) DataAnalise = dataSalva.Valor;
        if (campos.TryGetValue(nameof(IndiceCarencia), out var carencia)) IndiceCarencia = carencia.Indice;
        if (campos.TryGetValue(nameof(IndiceContratoNovo), out var contrato)) IndiceContratoNovo = contrato.Indice;
        if (campos.TryGetValue(nameof(IndiceProximoCedido), out var cedido)) IndiceProximoCedido = cedido.Indice;
        Demonstrativo = null;
        Erros.Limpar();
        return Result.Ok();
    }

    /// <summary>Campos a corrigir, no padrão das demais calculadoras: destaque no campo e aviso junto de Calcular.</summary>
    public ErrosDoFormulario Erros { get; } = new(new Dictionary<string, string>
    {
        [nameof(Saldo)] = "Saldo do extrato (R$)",
        [nameof(IndiceMes)] = "Mês de aniversário",
        [nameof(GarantiaBloqueada)] = "Garantia bloqueada (R$)",
        [nameof(MultaIncluida)] = "Multa incluída no saldo (R$)",
        [nameof(ParcelaComprometida)] = "Parcela anual cedida ao banco (R$)",
        [nameof(DataAnalise)] = "Data da análise"
    });

    private static bool IndiceValido(IReadOnlyDictionary<string, ValorCampoMobile> campos, string chave) =>
        !campos.TryGetValue(chave, out var valor) || valor is not null && valor.Indice is >= 0 and <= 2;

    [ObservableProperty] private string _saldo = "0,00";
    [ObservableProperty] private string _garantiaBloqueada = "0,00";
    [ObservableProperty] private string _multaIncluida = "0,00";
    [ObservableProperty] private string _parcelaComprometida = "0,00";
    [ObservableProperty] private bool _saldoIncluiGarantia;
    [ObservableProperty] private int _indiceMes = -1;
    public IReadOnlyList<string> Confirmacoes { get; } = ["Não informado", "Sim", "Não"];
    [ObservableProperty] private string _dataAnalise = DateTime.Today.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"));
    [ObservableProperty] private int _indiceCarencia;
    [ObservableProperty] private int _indiceContratoNovo;
    [ObservableProperty] private int _indiceProximoCedido;
    [ObservableProperty] private bool _exibirDetalhes;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharResumoCommand))]
    [NotifyPropertyChangedFor(nameof(TemResultado))]
    private DemonstrativoDto? _demonstrativo;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalcularCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharResumoCommand))]
    private bool _ocupado;

    public bool TemResultado => Demonstrativo is not null;
    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    public string ResultadoFormatado => Demonstrativo is { } demonstrativo
        ? demonstrativo.Resultado.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"))
        : string.Empty;

    partial void OnDemonstrativoChanged(DemonstrativoDto? value) => OnPropertyChanged(nameof(ResultadoFormatado));

    // Qualquer edição invalida a apuração anterior para não associar valores antigos ao formulário atual.
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Erros.Corrigido(e.PropertyName ?? string.Empty);
        if (e.PropertyName is nameof(Saldo) or nameof(GarantiaBloqueada) or nameof(MultaIncluida)
            or nameof(ParcelaComprometida) or nameof(SaldoIncluiGarantia) or nameof(IndiceMes)
            or nameof(DataAnalise) or nameof(IndiceCarencia) or nameof(IndiceContratoNovo) or nameof(IndiceProximoCedido))
        {
            Demonstrativo = null;
            Mensagem = null;
            FormularioAlterado?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand(CanExecute = nameof(PodeCalcular))]
    private async Task CalcularAsync(CancellationToken cancellationToken)
    {
        Demonstrativo = null;
        Mensagem = null;
        // Lê o formulário inteiro antes de avisar, para apontar de uma vez todos os campos a corrigir.
        Erros.Limpar();
        var saldo = Erros.Moeda(nameof(Saldo), Saldo);
        var garantia = Erros.Moeda(nameof(GarantiaBloqueada), GarantiaBloqueada);
        var multa = Erros.Moeda(nameof(MultaIncluida), MultaIncluida);
        var parcela = Erros.Moeda(nameof(ParcelaComprometida), ParcelaComprometida);
        var dataAnalise = Erros.Data(nameof(DataAnalise), DataAnalise);
        if (Erros.Apresentar())
            return;
        var analise = new EntradaAnaliseAntecipacaoFgts(dataAnalise, Confirmacao(IndiceCarencia), Confirmacao(IndiceContratoNovo),
            Confirmacao(IndiceProximoCedido));
        var pedido = new SimularSaqueAniversarioRequest(saldo, IndiceMes + 1, parcela, garantia, multa, SaldoIncluiGarantia, analise);
        try
        {
            Ocupado = true;
            cancellationToken.ThrowIfCancellationRequested();
            var resultado = await _simular.ExecutarAsync(pedido, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (resultado.Falhou)
            {
                // Uma regra que cita um campo o destaca; as demais falhas ficam na mensagem.
                if (!Erros.Regra(resultado.Erro))
                    Mensagem = resultado.Erro.Mensagem;
            }
            else
                Demonstrativo = resultado.Valor;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Sair da página cancela a operação sem apresentar erro de validação.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao simular saque-aniversário no aplicativo móvel.");
            Mensagem = "Não foi possível concluir a simulação. Tente novamente.";
        }
        finally
        {
            Ocupado = false;
        }
    }

    private bool PodeCalcular() => !Ocupado;
    private static ConfirmacaoAntecipacaoFgts Confirmacao(int indice) => indice switch
    {
        1 => ConfirmacaoAntecipacaoFgts.Sim,
        2 => ConfirmacaoAntecipacaoFgts.Nao,
        _ => ConfirmacaoAntecipacaoFgts.NaoInformado
    };
    private bool PodeCompartilharPdf() => Demonstrativo is not null && !Ocupado;

    [RelayCommand(CanExecute = nameof(PodeCompartilharPdf))]
    private async Task CompartilharPdfAsync(CancellationToken cancellationToken)
    {
        if (Demonstrativo is not { } demonstrativo)
            return;
        Mensagem = null;
        Ocupado = true;
        try
        {
            await _compartilhamento.CompartilharAsync(demonstrativo, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao gerar ou compartilhar PDF da simulação móvel.");
            Mensagem = "Não foi possível compartilhar o PDF. Confira o espaço disponível e tente novamente.";
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand(CanExecute = nameof(PodeCompartilharPdf))]
    private async Task CompartilharResumoAsync(CancellationToken cancellationToken)
    {
        if (Demonstrativo is not { } demonstrativo)
            return;
        Mensagem = null;
        try
        {
            await _compartilhamento.CompartilharResumoAsync(demonstrativo, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao compartilhar o resumo da simulação móvel.");
            Mensagem = "Não foi possível compartilhar o resumo. Tente novamente.";
        }
    }

}
