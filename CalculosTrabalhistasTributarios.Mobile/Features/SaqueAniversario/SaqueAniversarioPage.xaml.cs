using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;

namespace CalculosTrabalhistasTributarios.Mobile.Features.SaqueAniversario;

public partial class SaqueAniversarioPage : ContentPage
{
    private readonly SaqueAniversarioViewModel _viewModel;

    public SaqueAniversarioPage(SaqueAniversarioViewModel viewModel)
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
