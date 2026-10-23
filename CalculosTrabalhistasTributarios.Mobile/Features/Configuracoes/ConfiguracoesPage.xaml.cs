using CalculosTrabalhistasTributarios.Mobile.Presentation.Configuracoes;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Configuracoes;

public partial class ConfiguracoesPage : ContentPage
{
    public ConfiguracoesPage(ConfiguracoesMobileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
