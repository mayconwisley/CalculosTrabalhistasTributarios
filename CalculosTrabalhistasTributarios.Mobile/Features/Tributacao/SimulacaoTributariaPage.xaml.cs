using CalculosTrabalhistasTributarios.Mobile.Presentation.Tributacao;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Tributacao;

public partial class SimulacaoTributariaPage : ContentPage
{
    private readonly SimulacaoTributariaViewModel _viewModel;
    public SimulacaoTributariaPage(SimulacaoTributariaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }
    protected override void OnDisappearing()
    {
        _viewModel.CalcularCommand.Cancel();
        base.OnDisappearing();
    }
}
