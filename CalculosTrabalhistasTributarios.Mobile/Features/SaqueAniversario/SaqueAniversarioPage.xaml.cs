using CalculosTrabalhistasTributarios.Mobile.Features.Ajuda;
using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Services;

namespace CalculosTrabalhistasTributarios.Mobile.Features.SaqueAniversario;

public partial class SaqueAniversarioPage : ContentPage
{
    private readonly SaqueAniversarioViewModel _viewModel;

    public SaqueAniversarioPage(SaqueAniversarioViewModel viewModel, HistoricoMobileService historico, ManualMobileService manual)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        ToolbarItems.Add(new ToolbarItem("Salvar", null, async () => await historico.SalvarAsync(TipoCalculadoraMobile.SaqueAniversario, _viewModel)));
        ToolbarItems.Add(new ToolbarItem("Ajuda", null, async () => await Navigation.PushAsync(new AjudaPage(TipoCalculadoraMobile.SaqueAniversario, "Saque-aniversário do FGTS", manual))));
        // No menu de opções da barra, como o botão "Preencher exemplo" do desktop.
        ToolbarItems.Add(new ToolbarItem { Text = "Preencher exemplo", Order = ToolbarItemOrder.Secondary,
            Command = new Command(async () => await historico.PreencherExemploAsync(TipoCalculadoraMobile.SaqueAniversario, _viewModel)) });
    }

    protected override void OnDisappearing()
    {
        _viewModel.CalcularCommand.Cancel();
        base.OnDisappearing();
    }
}
