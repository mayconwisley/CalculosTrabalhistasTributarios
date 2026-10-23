using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Services;

namespace CalculosTrabalhistasTributarios.Mobile.Features.SaqueAniversario;

public partial class SaqueAniversarioPage : ContentPage
{
    private readonly SaqueAniversarioViewModel _viewModel;

    public SaqueAniversarioPage(SaqueAniversarioViewModel viewModel, HistoricoMobileService historico, ManualMobileService manual)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        ToolbarItems.Add(new ToolbarItem("Salvar", null, async () => await historico.SalvarAsync(TipoCalculadoraMobile.SaqueAniversario, _viewModel)));
        ToolbarItems.Add(new ToolbarItem("Ajuda", null, async () => await manual.AbrirAsync()));
    }

    protected override void OnDisappearing()
    {
        _viewModel.CalcularCommand.Cancel();
        base.OnDisappearing();
    }
}
