using CalculosTrabalhistasTributarios.Mobile.Features.Ajuda;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Services;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Trabalhista;

public partial class CalculadoraTrabalhistaPage : ContentPage
{
    private readonly CalculadoraTrabalhistaViewModel _viewModel;
    private readonly TipoCalculadoraMobile _tipo;
    private readonly HistoricoMobileService _historico;

    public CalculadoraTrabalhistaPage(CalculadoraTrabalhistaViewModel viewModel, TipoCalculadoraMobile tipo,
        HistoricoMobileService historico, ManualMobileService manual)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _tipo = tipo;
        _historico = historico;
        Title = viewModel.Titulo;
        ToolbarItems.Add(new ToolbarItem("Salvar", null, async () => await _historico.SalvarAsync(_tipo, _viewModel)));
        ToolbarItems.Add(new ToolbarItem("Ajuda", null, async () => await Navigation.PushAsync(new AjudaPage(_tipo, viewModel.Titulo, manual))));
        // No menu de opções da barra, como o botão "Preencher exemplo" do desktop.
        ToolbarItems.Add(new ToolbarItem { Text = "Preencher exemplo", Order = ToolbarItemOrder.Secondary,
            Command = new Command(async () => await _historico.PreencherExemploAsync(_tipo, _viewModel)) });
    }

    protected override void OnDisappearing()
    {
        _viewModel.CalcularCommand.Cancel();
        base.OnDisappearing();
    }
}
