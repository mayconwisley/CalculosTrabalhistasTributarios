using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Trabalhista;

public partial class CalculadoraTrabalhistaPage : ContentPage
{
    private readonly CalculadoraTrabalhistaViewModel _viewModel;

    public CalculadoraTrabalhistaPage(CalculadoraTrabalhistaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Title = viewModel.Titulo;
    }

    protected override void OnDisappearing()
    {
        _viewModel.CalcularCommand.Cancel();
        base.OnDisappearing();
    }
}
