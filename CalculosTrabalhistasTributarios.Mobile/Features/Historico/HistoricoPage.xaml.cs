using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Historico;

public partial class HistoricoPage : ContentPage
{
    private readonly HistoricoMobileService _historico;
    private readonly ILogger<HistoricoPage> _logger;
    private bool _ocupado;

    public HistoricoPage(HistoricoMobileService historico, ILogger<HistoricoPage> logger)
    {
        InitializeComponent();
        _historico = historico;
        _logger = logger;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ExecutarAsync(async () => await AtualizarAsync());
    }

    private Task AtualizarAsync() => AtualizarListaAsync();

    private async Task AtualizarListaAsync()
    {
        var salvos = await _historico.ListarAsync();
        Lista.ItemsSource = salvos;
        Primeiro.ItemsSource = salvos;
        Segundo.ItemsSource = salvos;
        Aviso.IsVisible = false;
    }

    private async void Comparar(object sender, EventArgs args)
    {
        if (Primeiro.SelectedItem is not CalculoSalvoDto a || Segundo.SelectedItem is not CalculoSalvoDto b || a.Id == b.Id)
        {
            Aviso.Text = "Selecione dois cálculos diferentes da mesma calculadora.";
            Aviso.IsVisible = true;
            return;
        }
        await ExecutarAsync(() => _historico.CompararAsync(a, b));
    }

    private async Task ExecutarAsync(Func<Task> acao)
    {
        if (_ocupado) return;
        _ocupado = true;
        try { await acao(); }
        catch (Exception erro)
        {
            _logger.LogError(erro, "Falha ao consultar ou alterar histórico móvel.");
            Aviso.Text = "Não foi possível acessar o histórico. Tente novamente.";
            Aviso.IsVisible = true;
        }
        finally { _ocupado = false; }
    }

    private async void Abrir(object sender, EventArgs args)
    {
        if ((sender as Button)?.BindingContext is CalculoSalvoDto salvo)
            await ExecutarAsync(() => _historico.AbrirAsync(salvo));
    }

    private async void Duplicar(object sender, EventArgs args)
    {
        if ((sender as Button)?.BindingContext is CalculoSalvoDto salvo)
            await ExecutarAsync(async () => { await _historico.DuplicarAsync(salvo); await AtualizarAsync(); });
    }

    private async void Renomear(object sender, EventArgs args)
    {
        if ((sender as Button)?.BindingContext is CalculoSalvoDto salvo)
            await ExecutarAsync(async () => { await _historico.RenomearAsync(salvo); await AtualizarAsync(); });
    }

    private async void Excluir(object sender, EventArgs args)
    {
        if ((sender as Button)?.BindingContext is CalculoSalvoDto salvo)
            await ExecutarAsync(async () => { await _historico.ExcluirAsync(salvo); await AtualizarAsync(); });
    }
}
