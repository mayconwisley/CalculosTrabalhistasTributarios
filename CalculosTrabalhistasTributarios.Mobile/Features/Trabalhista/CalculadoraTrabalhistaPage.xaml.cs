using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Services;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Trabalhista;

public partial class CalculadoraTrabalhistaPage : ContentPage
{
    private readonly CalculadoraTrabalhistaViewModel _viewModel;
    private readonly TipoCalculadoraMobile _tipo;
    private readonly HistoricoMobileService _historico;

    public CalculadoraTrabalhistaPage(CalculadoraTrabalhistaViewModel viewModel, TipoCalculadoraMobile tipo,
        HistoricoMobileService historico, ManualMobileService manual)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _tipo = tipo;
        _historico = historico;
        Title = viewModel.Titulo;
        ToolbarItems.Add(new ToolbarItem("Salvar", null, async () => await _historico.SalvarAsync(_tipo, _viewModel)));
        ToolbarItems.Add(new ToolbarItem("Ajuda", null, async () => await manual.AbrirAsync()));
    }

    protected override void OnDisappearing()
    {
        _viewModel.CalcularCommand.Cancel();
        base.OnDisappearing();
    }
}
