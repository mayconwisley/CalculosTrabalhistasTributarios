using CalculosTrabalhistasTributarios.Mobile.Presentation.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Services;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Tributacao;

public partial class SimulacaoTributariaPage : ContentPage
{
    private readonly SimulacaoTributariaViewModel _viewModel;
    public SimulacaoTributariaPage(SimulacaoTributariaViewModel viewModel, HistoricoMobileService historico, ManualMobileService manual)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        ToolbarItems.Add(new ToolbarItem("Salvar", null, async () => await historico.SalvarAsync(TipoCalculadoraMobile.SimulacaoTributaria, _viewModel)));
        ToolbarItems.Add(new ToolbarItem("Ajuda", null, async () => await manual.AbrirAsync()));
    }
    protected override void OnDisappearing()
    {
        _viewModel.CalcularCommand.Cancel();
        base.OnDisappearing();
    }
}
