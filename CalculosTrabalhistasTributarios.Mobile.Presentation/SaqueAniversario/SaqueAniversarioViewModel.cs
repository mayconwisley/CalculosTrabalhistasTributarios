using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;

public sealed partial class SaqueAniversarioViewModel : ObservableObject
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

    [ObservableProperty] private string _saldo = "0,00";
    [ObservableProperty] private string _garantiaBloqueada = "0,00";
    [ObservableProperty] private string _multaIncluida = "0,00";
    [ObservableProperty] private string _parcelaComprometida = "0,00";
    [ObservableProperty] private bool _saldoIncluiGarantia;
    [ObservableProperty] private int _indiceMes = -1;
    [ObservableProperty] private bool _exibirDetalhes;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
    [NotifyPropertyChangedFor(nameof(TemResultado))]
    private DemonstrativoDto? _demonstrativo;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalcularCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
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
        if (e.PropertyName is nameof(Saldo) or nameof(GarantiaBloqueada) or nameof(MultaIncluida)
            or nameof(ParcelaComprometida) or nameof(SaldoIncluiGarantia) or nameof(IndiceMes))
        {
            Demonstrativo = null;
            Mensagem = null;
        }
    }

    [RelayCommand(CanExecute = nameof(PodeCalcular))]
    private async Task CalcularAsync(CancellationToken cancellationToken)
    {
        Demonstrativo = null;
        Mensagem = null;
        if (!LerValor(Saldo, "Saldo do extrato", out var saldo)
            || !LerValor(GarantiaBloqueada, "Garantia bloqueada", out var garantia)
            || !LerValor(MultaIncluida, "Multa rescisória incluída", out var multa)
            || !LerValor(ParcelaComprometida, "Parcela comprometida", out var parcela))
            return;

        var pedido = new SimularSaqueAniversarioRequest(saldo, IndiceMes + 1, parcela, garantia, multa, SaldoIncluiGarantia);
        try
        {
            Ocupado = true;
            cancellationToken.ThrowIfCancellationRequested();
            var resultado = await _simular.ExecutarAsync(pedido, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (resultado.Falhou)
                Mensagem = resultado.Erro.Mensagem;
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

    private bool LerValor(string texto, string campo, out decimal valor)
    {
        if (LeituraNumerica.TentarLer(texto, out valor))
            return true;
        Mensagem = $"{campo}: informe um valor numérico, por exemplo 3.500,00. Informe 0,00 quando não houver valor.";
        return false;
    }
}
