using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Historico;

public partial class HistoricoPage : ContentPage
{
    private readonly HistoricoMobileViewModel _viewModel;
    public HistoricoPage(HistoricoMobileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.AtualizarCommand.ExecuteAsync(null);
    }
    protected override void OnDisappearing()
    {
        _viewModel.InterromperConsulta();
        base.OnDisappearing();
    }
}
