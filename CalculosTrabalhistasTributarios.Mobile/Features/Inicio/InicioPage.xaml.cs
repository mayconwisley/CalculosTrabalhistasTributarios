using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Inicio;

public partial class InicioPage : ContentPage
{
    private readonly InicioViewModel _viewModel;

    public InicioPage(InicioViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Os recentes e a data das tabelas mudam nas outras telas; ao voltar, são relidos.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Na primeira exibição, a janela ainda está trocando a abertura pela tela inicial: o aviso pedido agora iria para a
        // página que sai e nunca seria fechado. Ele espera o próximo ciclo da interface, sem segurar os recentes.
        Dispatcher.Dispatch(async () => await _viewModel.MostrarAvisoDeSimulacaoAsync());
        await _viewModel.AtualizarBoasVindasCommand.ExecuteAsync(null);
    }
}
