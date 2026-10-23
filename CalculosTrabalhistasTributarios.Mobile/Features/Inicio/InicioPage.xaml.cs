using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Inicio;

public partial class InicioPage : ContentPage
{
    public InicioPage(InicioViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
