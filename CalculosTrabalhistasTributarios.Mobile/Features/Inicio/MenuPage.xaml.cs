using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Services;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Inicio;

public partial class MenuPage : ContentPage
{
    private readonly NavegadorMenu _navegador;
    private readonly ManualMobileService _manual;

    public MenuPage(InicioViewModel viewModel, NavegadorMenu navegador, ManualMobileService manual)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _navegador = navegador;
        _manual = manual;
    }

    private async void AbrirHistorico(object sender, EventArgs args) => await _navegador.AbrirHistoricoAsync();
    private async void AbrirManual(object sender, EventArgs args) => await _manual.AbrirAsync();
}
