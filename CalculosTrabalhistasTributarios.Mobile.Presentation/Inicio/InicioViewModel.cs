using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

public sealed partial class InicioViewModel(INavegacaoCalculadoras navegacao, ILogger<InicioViewModel> logger) : ObservableObject
{
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AbrirTributariaCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbrirSaqueAniversarioCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbrirDecimoTerceiroCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbrirFeriasCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbrirHorasExtrasCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbrirRescisaoCommand))]
    private bool _ocupado;

    private bool PodeAbrir() => !Ocupado;
    [RelayCommand(CanExecute = nameof(PodeAbrir))]
    private Task AbrirTributariaAsync() => AbrirAsync(TipoCalculadoraMobile.SimulacaoTributaria);
    [RelayCommand(CanExecute = nameof(PodeAbrir))]
    private Task AbrirSaqueAniversarioAsync() => AbrirAsync(TipoCalculadoraMobile.SaqueAniversario);
    [RelayCommand(CanExecute = nameof(PodeAbrir))]
    private Task AbrirDecimoTerceiroAsync() => AbrirAsync(TipoCalculadoraMobile.DecimoTerceiro);
    [RelayCommand(CanExecute = nameof(PodeAbrir))]
    private Task AbrirFeriasAsync() => AbrirAsync(TipoCalculadoraMobile.Ferias);
    [RelayCommand(CanExecute = nameof(PodeAbrir))]
    private Task AbrirHorasExtrasAsync() => AbrirAsync(TipoCalculadoraMobile.HorasExtras);
    [RelayCommand(CanExecute = nameof(PodeAbrir))]
    private Task AbrirRescisaoAsync() => AbrirAsync(TipoCalculadoraMobile.Rescisao);

    private async Task AbrirAsync(TipoCalculadoraMobile tipo)
    {
        Ocupado = true;
        Mensagem = null;
        try { await navegacao.AbrirAsync(tipo); }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao abrir calculadora móvel {Tipo}.", tipo);
            Mensagem = "Não foi possível abrir a calculadora. Tente novamente.";
        }
        finally { Ocupado = false; }
    }
}
