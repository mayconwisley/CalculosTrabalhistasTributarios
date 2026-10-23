using CalculosTrabalhistasTributarios.Mobile.Presentation.Tabelas;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Tabelas;

public partial class TabelaPage : ContentPage
{
    private readonly TabelaMobileViewModel _viewModel;

    public TabelaPage(TabelaMobileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Title = viewModel.Titulo;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CarregarCommand.ExecuteAsync(null);
    }
}
