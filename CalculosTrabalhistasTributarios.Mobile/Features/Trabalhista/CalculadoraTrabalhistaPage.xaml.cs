using System.ComponentModel;
using CalculosTrabalhistasTributarios.Mobile.Views;
using CalculosTrabalhistasTributarios.Mobile.Features.Ajuda;
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
        ToolbarItems.Add(new ToolbarItem("Ajuda", null, async () => await Navigation.PushAsync(new AjudaPage(_tipo, viewModel.Titulo, manual))));
        // No menu de opções da barra, como o botão "Preencher exemplo" do desktop.
        ToolbarItems.Add(new ToolbarItem { Text = "Preencher exemplo", Order = ToolbarItemOrder.Secondary,
            Command = new Command(async () => await _historico.PreencherExemploAsync(_tipo, _viewModel)) });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged += AoAlterarFormulario;
    }

    private void AlternarSecao(object? sender, EventArgs args)
    {
        if (sender is Button { BindingContext: SecaoCamposMobile secao }) secao.Expandida = !secao.Expandida;
    }

    private async void IrAoResultado(object? sender, EventArgs args) => await RolagemFormulario.ScrollToAsync(Resultado, ScrollToPosition.Start, false);

    private void AoAlterarFormulario(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(CalculadoraTrabalhistaViewModel.PrimeiroCampoComErro) && _viewModel.PrimeiroCampoComErro is { } campo)
            Dispatcher.Dispatch(async () =>
            {
                var controle = ApoioFormularioMobile.Descendentes(this).OfType<CampoFormularioView>().FirstOrDefault(v => ReferenceEquals(v.Campo, campo));
                if (controle is null) return;
                await RolagemFormulario.ScrollToAsync(controle, ScrollToPosition.Start, false);
                controle.FocarEntrada();
                SemanticScreenReader.Default.Announce(campo.Erro ?? "Confira o campo.");
            });
        else if (args.PropertyName == nameof(CalculadoraTrabalhistaViewModel.Demonstrativo) && _viewModel.TemResultado)
            Dispatcher.Dispatch(async () => await RolagemFormulario.ScrollToAsync(Resultado, ScrollToPosition.Start, false));
    }

    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= AoAlterarFormulario;
        _viewModel.CalcularCommand.Cancel();
        base.OnDisappearing();
    }
}
