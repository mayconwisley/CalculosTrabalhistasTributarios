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
        // A lista fica abaixo do formulário; ao escolher uma faixa, mostra os campos preenchidos.
        viewModel.PropertyChanged += async (_, args) =>
        {
            if (args.PropertyName == nameof(TabelaMobileViewModel.Selecionado) && viewModel.Selecionado is not null)
                await Rolagem.ScrollToAsync(0, 0, true);
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CarregarCommand.ExecuteAsync(null);
    }
}
