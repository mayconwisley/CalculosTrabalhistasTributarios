using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Historico;

public partial class HistoricoPage : ContentPage
{
    private readonly HistoricoMobileService _historico;
    private readonly ILogger<HistoricoPage> _logger;
    private bool _ocupado;
    private bool _preenchendoFiltros;
    private IReadOnlyList<CalculoSalvoDto> _salvos = [];

    public HistoricoPage(HistoricoMobileService historico, ILogger<HistoricoPage> logger)
    {
        InitializeComponent();
        _historico = historico;
        _logger = logger;
        _preenchendoFiltros = true;
        Periodo.ItemsSource = FiltroHistoricoMobile.Periodos.ToList();
        Periodo.SelectedIndex = 0;
        Ordem.ItemsSource = FiltroHistoricoMobile.Ordens.ToList();
        Ordem.SelectedIndex = 0;
        _preenchendoFiltros = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ExecutarAsync(async () => await AtualizarAsync());
    }

    private Task AtualizarAsync() => AtualizarListaAsync();

    private async Task AtualizarListaAsync()
    {
        // O Picker exige IList; o SQLite é lido fora da thread da tela.
        _salvos = (await Task.Run(_historico.ListarAsync)).ToList();
        Primeiro.ItemsSource = _salvos.ToList();
        Segundo.ItemsSource = _salvos.ToList();
        // Mantém a calculadora escolhida enquanto ela ainda tiver cálculos salvos.
        var calculadora = Calculadora.SelectedItem as string;
        var calculadoras = FiltroHistoricoMobile.Calculadoras(_salvos).ToList();
        _preenchendoFiltros = true;
        Calculadora.ItemsSource = calculadoras;
        Calculadora.SelectedIndex = Math.Max(0, calculadoras.IndexOf(calculadora ?? FiltroHistoricoMobile.TodasAsCalculadoras));
        _preenchendoFiltros = false;
        Aviso.IsVisible = false;
        AplicarFiltros();
    }

    private void Filtrar(object? sender, EventArgs args)
    {
        if (!_preenchendoFiltros) AplicarFiltros();
    }

    private void AplicarFiltros()
    {
        var encontrados = FiltroHistoricoMobile.Aplicar(_salvos, Busca.Text, Calculadora.SelectedItem as string,
            Math.Max(0, Periodo.SelectedIndex), Math.Max(0, Ordem.SelectedIndex), DateTime.Now);
        Lista.ItemsSource = encontrados;
        Resumo.Text = _salvos.Count == 0 ? string.Empty
            : encontrados.Count == _salvos.Count ? $"{_salvos.Count} {(_salvos.Count == 1 ? "cálculo salvo" : "cálculos salvos")}"
            : $"{encontrados.Count} de {_salvos.Count} cálculos";
        Vazio.Text = _salvos.Count == 0 ? "Nenhum cálculo salvo ainda. Use Salvar na tela de uma calculadora."
            : "Nenhum cálculo atende à busca e aos filtros. Altere ou limpe a busca.";
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
