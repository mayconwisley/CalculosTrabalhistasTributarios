using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;

public sealed partial class SaqueAniversarioViewModel : ObservableObject
{
    private readonly ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest> _simular;
    private readonly ILogger<SaqueAniversarioViewModel> _logger;

    public SaqueAniversarioViewModel(ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest> simular,
        ILogger<SaqueAniversarioViewModel> logger)
    {
        _simular = simular;
        _logger = logger;
    }

    public IReadOnlyList<string> Meses { get; } = CultureInfo.GetCultureInfo("pt-BR")
        .DateTimeFormat.MonthNames.Take(12).ToArray();

    [ObservableProperty] private string _saldo = "0,00";
    [ObservableProperty] private string _garantiaBloqueada = "0,00";
    [ObservableProperty] private string _multaIncluida = "0,00";
    [ObservableProperty] private string _parcelaComprometida = "0,00";
    [ObservableProperty] private bool _saldoIncluiGarantia;
    [ObservableProperty] private int _indiceMes = -1;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private DemonstrativoDto? _demonstrativo;

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

    [RelayCommand]
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
    }

    private bool LerValor(string texto, string campo, out decimal valor)
    {
        if (LeituraNumerica.TentarLer(texto, out valor))
            return true;
        Mensagem = $"{campo}: informe um valor numérico, por exemplo 3.500,00. Informe 0,00 quando não houver valor.";
        return false;
    }
}
