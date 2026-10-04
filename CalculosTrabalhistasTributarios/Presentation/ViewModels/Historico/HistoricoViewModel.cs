using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

/// <summary>
/// Aba Histórico da tela principal: os cálculos salvos, com busca pelo nome ou pela calculadora, e as ações de abrir,
/// duplicar, renomear e excluir.
/// </summary>
public sealed class HistoricoViewModel : ViewModelBase
{
    private static readonly CompareInfo Comparacao = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
    private readonly IHistoricoCalculos _historico;
    private readonly IWindowNavigator _navegador;
    private readonly INomeDialogService _dialogo;
    private readonly IUserNotifier _notificador;
    private IReadOnlyList<CalculoSalvoDto> _todos = [];
    private string _filtro = string.Empty;

    public HistoricoViewModel(IHistoricoCalculos historico, IWindowNavigator navegador, INomeDialogService dialogo, IUserNotifier notificador)
    {
        _historico = historico;
        _navegador = navegador;
        _dialogo = dialogo;
        _notificador = notificador;
        AtualizarCommand = new AsyncRelayCommand(CarregarAsync);
        AbrirCommand = new RelayCommand(parametro => _ = Executar(parametro, AbrirAsync));
        DuplicarCommand = new RelayCommand(parametro => _ = Executar(parametro, DuplicarAsync));
        RenomearCommand = new RelayCommand(parametro => _ = Executar(parametro, RenomearAsync));
        ExcluirCommand = new RelayCommand(parametro => _ = Executar(parametro, ExcluirAsync));
    }

    public ObservableCollection<ItemHistoricoViewModel> Itens { get; } = [];
    public ICommand AtualizarCommand { get; }
    public ICommand AbrirCommand { get; }
    public ICommand DuplicarCommand { get; }
    public ICommand RenomearCommand { get; }
    public ICommand ExcluirCommand { get; }

    public string Filtro
    {
        get => _filtro;
        set
        {
            if (SetProperty(ref _filtro, value))
                Filtrar();
        }
    }

    public bool TemItens => Itens.Count > 0;

    /// <summary>Mensagem da lista vazia: sem cálculos salvos ou sem nenhum que atenda à busca.</summary>
    public string MensagemVazia => _todos.Count == 0
        ? "Nenhum cálculo salvo ainda. Em qualquer calculadora, selecione Salvar no histórico para guardar o formulário com um nome, como o do empregado ou o número do processo."
        : $"Nenhum cálculo salvo tem “{Filtro.Trim()}” no nome ou na calculadora.";

    public string Resumo => _todos.Count switch
    {
        0 => string.Empty,
        1 => "1 cálculo salvo",
        _ when Itens.Count < _todos.Count => $"{Itens.Count} de {_todos.Count} cálculos salvos",
        _ => $"{_todos.Count} cálculos salvos"
    };

    public async Task CarregarAsync()
    {
        try
        {
            _todos = await _historico.ListarAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro("Não foi possível ler o histórico de cálculos.", exception);
            _todos = [];
        }
        Filtrar();
    }

    private void Filtrar()
    {
        var filtro = Filtro.Trim();
        Itens.Clear();
        foreach (var calculo in _todos.Where(calculo => filtro.Length == 0 || Contem(calculo.Nome, filtro) || Contem(calculo.Calculadora, filtro)))
            Itens.Add(new ItemHistoricoViewModel(calculo));
        OnPropertyChanged(nameof(TemItens));
        OnPropertyChanged(nameof(MensagemVazia));
        OnPropertyChanged(nameof(Resumo));
    }

    // A busca ignora maiúsculas e acentos: "pensao" encontra "Pensão".
    private static bool Contem(string texto, string busca) => Comparacao.IndexOf(texto, busca, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    private async Task Executar(object? parametro, Func<CalculoSalvoDto, Task> acao)
    {
        if (parametro is not ItemHistoricoViewModel item)
            return;
        try
        {
            await acao(item.Calculo);
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro($"Não foi possível concluir a ação com “{item.Nome}”.", exception);
        }
        await CarregarAsync();
    }

    private async Task AbrirAsync(CalculoSalvoDto calculo)
    {
        // Lê de novo: o cálculo pode ter sido alterado ou excluído em outra janela.
        var atual = await _historico.ObterAsync(calculo.Id, CancellationToken.None);
        if (atual is null)
        {
            _notificador.MostrarAviso($"O cálculo “{calculo.Nome}” não está mais no histórico.", "Histórico");
            return;
        }
        var aberto = await _navegador.AbrirCalculoSalvoAsync(atual);
        if (aberto.Falhou)
            _notificador.MostrarFalha(aberto.Erro, $"Não foi possível abrir “{calculo.Nome}”.");
    }

    private async Task DuplicarAsync(CalculoSalvoDto calculo)
    {
        var copia = await _historico.DuplicarAsync(calculo.Id, CancellationToken.None);
        if (copia.Falhou)
            _notificador.MostrarFalha(copia.Erro, $"Não foi possível duplicar “{calculo.Nome}”.");
    }

    private async Task RenomearAsync(CalculoSalvoDto calculo)
    {
        if (_dialogo.SolicitarNome("Renomear cálculo", "Renomear", calculo.Nome) is { } escolha && escolha.Nome != calculo.Nome)
            await _historico.RenomearAsync(calculo.Id, escolha.Nome, CancellationToken.None);
    }

    private async Task ExcluirAsync(CalculoSalvoDto calculo)
    {
        if (_notificador.Confirmar($"Excluir “{calculo.Nome}” do histórico? Essa ação não pode ser desfeita.", "Excluir cálculo"))
            await _historico.ExcluirAsync(calculo.Id, CancellationToken.None);
    }
}
