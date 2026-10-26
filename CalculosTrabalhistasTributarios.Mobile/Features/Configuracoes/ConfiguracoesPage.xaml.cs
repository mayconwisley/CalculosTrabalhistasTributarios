using CalculosTrabalhistasTributarios.Mobile.Presentation.Configuracoes;
using CalculosTrabalhistasTributarios.Presentation.Documentos;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Configuracoes;

public partial class ConfiguracoesPage : ContentPage
{
    public ConfiguracoesPage(ConfiguracoesMobileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void AbrirDocumento(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: TipoDocumentoLegal tipo })
            await Navigation.PushAsync(new DocumentoLegalPage(tipo));
    }
}
