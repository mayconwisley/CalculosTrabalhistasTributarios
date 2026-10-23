using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Abertura;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Abertura;

public partial class AberturaPage : ContentPage
{
    private readonly AtualizacaoTabelasViewModel _viewModel;
    private readonly PreparadorBancoLocal _banco;
    private bool _iniciada;

    public event EventHandler? Preparada;

    public AberturaPage(AtualizacaoTabelasViewModel viewModel, PreparadorBancoLocal banco)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _banco = banco;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_iniciada) return;
        _iniciada = true;
        await PrepararAsync();
    }

    private async void TentarNovamente(object sender, EventArgs args) => await PrepararAsync();

    private async Task PrepararAsync()
    {
        if (await _viewModel.AbrirAsync(_banco.PrepararAsync, DateTime.Now)) Preparada?.Invoke(this, EventArgs.Empty);
    }
}
