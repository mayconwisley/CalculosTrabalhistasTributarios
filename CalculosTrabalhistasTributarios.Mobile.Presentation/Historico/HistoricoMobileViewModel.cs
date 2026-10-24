using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;

public sealed partial class HistoricoMobileViewModel(IConsultaHistoricoCalculos consulta, IInteracaoHistoricoMobile interacao,
    ILogger<HistoricoMobileViewModel> logger) : ObservableObject
{
    private const int TamanhoPagina = 20;
    private CancellationTokenSource? _consulta;
    private int _pagina;
    private int _total;
    private bool _ajustandoFiltros;
    [ObservableProperty] private IReadOnlyList<ResumoCalculoSalvoDto> _itens = [];
    [ObservableProperty] private IReadOnlyList<string> _calculadoras = [FiltroHistoricoMobile.TodasAsCalculadoras];
    [ObservableProperty] private string _busca = "";
    [ObservableProperty] private string _calculadora = FiltroHistoricoMobile.TodasAsCalculadoras;
    [ObservableProperty] private int _periodo;
    [ObservableProperty] private int _ordem;
    [ObservableProperty] private string _resumo = "";
    [ObservableProperty] private string _resumoPagina = "";
    [ObservableProperty] private string _vazio = "Nenhum cálculo salvo ainda.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemMensagem))] private string? _mensagem;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Ocupado))] private bool _carregando;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Ocupado))] private bool _executandoAcao;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Selecao))] private ResumoCalculoSalvoDto? _primeiro;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Selecao))] private ResumoCalculoSalvoDto? _segundo;
    public IReadOnlyList<string> Periodos => FiltroHistoricoMobile.Periodos;
    public IReadOnlyList<string> Ordens => FiltroHistoricoMobile.Ordens;
    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);
    public bool Ocupado => Carregando || ExecutandoAcao;
    public string Selecao => Primeiro is null ? "Selecione até dois cálculos da mesma calculadora na lista."
        : Segundo is null ? $"Selecionado: {Primeiro.Nome}. Selecione outro para comparar." : $"Comparar: {Primeiro.Nome} e {Segundo.Nome}.";
    private bool PodeExecutar() => !Ocupado;
    private bool PodeComparar() => PodeExecutar() && Primeiro is not null && Segundo is not null;
    private bool PodeAnterior() => PodeExecutar() && _pagina > 0;
    private bool PodeProxima() => PodeExecutar() && (_pagina + 1) * TamanhoPagina < _total;
    partial void OnBuscaChanged(string value) => AgendarFiltro();
    partial void OnCalculadoraChanged(string value) => AgendarFiltro();
    partial void OnPeriodoChanged(int value) => AgendarFiltro();
    partial void OnOrdemChanged(int value) => AgendarFiltro();
    partial void OnPrimeiroChanged(ResumoCalculoSalvoDto? value) => CompararCommand.NotifyCanExecuteChanged();
    partial void OnSegundoChanged(ResumoCalculoSalvoDto? value) => CompararCommand.NotifyCanExecuteChanged();
    partial void OnCarregandoChanged(bool value) => AtualizarComandos();
    partial void OnExecutandoAcaoChanged(bool value) => AtualizarComandos();
    private void AtualizarComandos()
    {
        AtualizarCommand.NotifyCanExecuteChanged(); AnteriorCommand.NotifyCanExecuteChanged(); ProximaCommand.NotifyCanExecuteChanged();
        AbrirCommand.NotifyCanExecuteChanged(); DuplicarCommand.NotifyCanExecuteChanged(); RenomearCommand.NotifyCanExecuteChanged();
        ExcluirCommand.NotifyCanExecuteChanged(); SelecionarCommand.NotifyCanExecuteChanged(); CompararCommand.NotifyCanExecuteChanged();
    }
    private void AgendarFiltro()
    {
        if (_ajustandoFiltros) return;
        _pagina = 0;
        _ = ConsultarAsync(adiar: true);
    }
    public void InterromperConsulta() => _consulta?.Cancel();

    [RelayCommand(CanExecute = nameof(PodeExecutar))]
    private Task AtualizarAsync() { _pagina = 0; return ConsultarAsync(); }
    [RelayCommand(CanExecute = nameof(PodeAnterior))]
    private Task AnteriorAsync() { _pagina--; return ConsultarAsync(); }
    [RelayCommand(CanExecute = nameof(PodeProxima))]
    private Task ProximaAsync() { _pagina++; return ConsultarAsync(); }

    private async Task ConsultarAsync(bool adiar = false)
    {
        _consulta?.Cancel();
        using var cancelamento = new CancellationTokenSource();
        _consulta = cancelamento;
        Carregando = true;
        Mensagem = null;
        try
        {
            if (adiar) await Task.Delay(300, cancelamento.Token);
            var hoje = DateTime.Today;
            DateTime? desde = Periodo switch { 1 => hoje.AddDays(-30), 2 => new(hoje.Year, 1, 1), _ => null };
            DateTime? ate = Periodo == 2 && hoje.Year < 9999 ? new(hoje.Year + 1, 1, 1) : null;
            var pedido = new ConsultaHistoricoDto(Busca, Calculadora == FiltroHistoricoMobile.TodasAsCalculadoras ? null : Calculadora,
                desde, ate, (OrdemHistorico)Math.Clamp(Ordem, 0, 2), _pagina * TamanhoPagina, TamanhoPagina);
            var resultado = await consulta.ConsultarAsync(pedido, cancelamento.Token);
            // Algumas fontes só observam cancelamento no fim; nunca publique uma consulta substituída.
            cancelamento.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_consulta, cancelamento)) return;
            _total = resultado.Total;
            if (_pagina > 0 && pedido.Deslocamento >= _total)
            {
                _pagina = Math.Max(0, (_total - 1) / TamanhoPagina);
                resultado = await consulta.ConsultarAsync(pedido with { Deslocamento = _pagina * TamanhoPagina }, cancelamento.Token);
                cancelamento.Token.ThrowIfCancellationRequested();
            }
            Itens = resultado.Itens;
            _ajustandoFiltros = true;
            var selecionada = Calculadora;
            string[] opcoes = [FiltroHistoricoMobile.TodasAsCalculadoras, .. resultado.Calculadoras];
            if (!Calculadoras.SequenceEqual(opcoes)) Calculadoras = opcoes;
            Calculadora = selecionada;
            _ajustandoFiltros = false;
            Resumo = $"{resultado.Total} de {resultado.TotalGeral} cálculos";
            ResumoPagina = $"Página {_pagina + 1} de {Math.Max(1, (resultado.Total + TamanhoPagina - 1) / TamanhoPagina)}";
            Vazio = resultado.TotalGeral == 0 ? "Nenhum cálculo salvo ainda. Use Salvar em uma calculadora."
                : "Nenhum cálculo atende aos filtros. Altere ou limpe a busca.";
        }
        catch (OperationCanceledException) when (cancelamento.IsCancellationRequested) { }
        catch (Exception erro)
        {
            if (!ReferenceEquals(_consulta, cancelamento)) return;
            logger.LogError(erro, "Falha ao consultar histórico móvel.");
            Itens = []; _total = 0;
            Resumo = ""; ResumoPagina = ""; Vazio = "Toque em Atualizar para tentar novamente.";
            Mensagem = "Não foi possível carregar o histórico. Toque em Atualizar para tentar novamente.";
        }
        finally
        {
            if (ReferenceEquals(_consulta, cancelamento)) { _consulta = null; Carregando = false; AtualizarComandos(); }
        }
    }

    [RelayCommand(CanExecute = nameof(PodeExecutar))]
    private void Selecionar(ResumoCalculoSalvoDto salvo)
    {
        Mensagem = null;
        if (Primeiro?.Id == salvo.Id) { Primeiro = Segundo; Segundo = null; }
        else if (Segundo?.Id == salvo.Id) Segundo = null;
        else if (Primeiro is null) Primeiro = salvo;
        else if (Primeiro.Tipo != salvo.Tipo) Mensagem = "Selecione dois cálculos da mesma calculadora.";
        else if (Segundo is null) Segundo = salvo;
        else Mensagem = "Há dois cálculos selecionados. Toque novamente em um deles para removê-lo da comparação.";
    }
    [RelayCommand(CanExecute = nameof(PodeComparar))]
    private Task CompararAsync() => Primeiro is { } a && Segundo is { } b ? ExecutarAsync(() => interacao.CompararAsync(a, b)) : Task.CompletedTask;
    [RelayCommand(CanExecute = nameof(PodeExecutar))]
    private Task AbrirAsync(ResumoCalculoSalvoDto salvo) => ExecutarAsync(() => interacao.AbrirAsync(salvo));
    [RelayCommand(CanExecute = nameof(PodeExecutar))]
    private Task DuplicarAsync(ResumoCalculoSalvoDto salvo) => AlterarAsync(() => interacao.DuplicarAsync(salvo));
    [RelayCommand(CanExecute = nameof(PodeExecutar))]
    private Task RenomearAsync(ResumoCalculoSalvoDto salvo) => AlterarAsync(() => interacao.RenomearAsync(salvo));
    [RelayCommand(CanExecute = nameof(PodeExecutar))]
    private Task ExcluirAsync(ResumoCalculoSalvoDto salvo) => AlterarAsync(() => interacao.ExcluirAsync(salvo));
    private Task AlterarAsync(Func<Task> acao) => ExecutarAsync(async () =>
    {
        await acao(); Primeiro = null; Segundo = null;
        await ConsultarAsync();
    });
    private async Task ExecutarAsync(Func<Task> acao)
    {
        if (Ocupado) return;
        ExecutandoAcao = true; Mensagem = null;
        try { await acao(); }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha ao executar ação no histórico móvel.");
            Mensagem = "Não foi possível concluir a ação. Tente novamente.";
        }
        finally { ExecutandoAcao = false; }
    }
}
