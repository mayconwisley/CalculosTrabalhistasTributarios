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
    private readonly IComparacaoHistoricoService _comparacao;
    private IReadOnlyList<CalculoSalvoDto> _todos = [];
    private IReadOnlyList<ItemHistoricoViewModel> _itens = [];
    private string _filtro = string.Empty;
    private string _calculadoraSelecionada = "Todas as calculadoras";
    private string _periodoSelecionado = "Qualquer data";
    private string _ordemSelecionada = "Mais recentes";

    public HistoricoViewModel(IHistoricoCalculos historico, IWindowNavigator navegador, INomeDialogService dialogo, IUserNotifier notificador, IComparacaoHistoricoService comparacao)
    {
        _historico = historico;
        _navegador = navegador;
        _dialogo = dialogo;
        _notificador = notificador;
        _comparacao = comparacao;
        AtualizarCommand = new AsyncRelayCommand(CarregarAsync);
        AbrirCommand = new RelayCommand(parametro => _ = Executar(parametro, AbrirAsync));
        DuplicarCommand = new RelayCommand(parametro => _ = Executar(parametro, DuplicarAsync));
        RenomearCommand = new RelayCommand(parametro => _ = Executar(parametro, RenomearAsync));
        ExcluirCommand = new RelayCommand(parametro => _ = Executar(parametro, ExcluirAsync));
        CompararCommand = new AsyncRelayCommand(CompararAsync, PodeComparar);
    }

    public ObservableCollection<ItemHistoricoViewModel> Itens { get; } = [];
    public ICommand AtualizarCommand { get; }
    public ICommand AbrirCommand { get; }
    public ICommand DuplicarCommand { get; }
    public ICommand RenomearCommand { get; }
    public ICommand ExcluirCommand { get; }
    public ICommand CompararCommand { get; }
    public ObservableCollection<string> CalculadorasDisponiveis { get; } = ["Todas as calculadoras"];
    public IReadOnlyList<string> Periodos { get; } = ["Qualquer data", "Últimos 30 dias", "Este ano"];
    public IReadOnlyList<string> Ordens { get; } = ["Mais recentes", "Mais antigos", "Nome"];
    public string CalculadoraSelecionada
    {
        get => _calculadoraSelecionada;
        set { if (value is not null && SetProperty(ref _calculadoraSelecionada, value)) Filtrar(); }
    }

    public string PeriodoSelecionado
    {
        get => _periodoSelecionado;
        set { if (value is not null && SetProperty(ref _periodoSelecionado, value)) Filtrar(); }
    }

    public string OrdemSelecionada
    {
        get => _ordemSelecionada;
        set { if (value is not null && SetProperty(ref _ordemSelecionada, value)) Filtrar(); }
    }
    public string SituacaoComparacao => _itens.Count(item => item.Selecionado) switch
    {
        0 => "Selecione dois cálculos da mesma calculadora para comparar as entradas.",
        1 => "Selecione mais um cálculo da mesma calculadora.",
        2 when PodeComparar() => "Dois cálculos selecionados. A comparação mostra os dados informados.",
        2 => "Selecione cálculos da mesma calculadora.",
        _ => "Selecione apenas dois cálculos."
    };

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
        : "Nenhum cálculo corresponde à busca e aos filtros selecionados.";

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
        _itens = _todos.Select(calculo => new ItemHistoricoViewModel(calculo)).ToArray();
        foreach (var item in _itens)
            item.PropertyChanged += (_, argumentos) => { if (argumentos.PropertyName == nameof(ItemHistoricoViewModel.Selecionado)) AtualizarComparacao(); };
        var tipos = _todos.Select(calculo => calculo.Calculadora).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(nome => nome).ToArray();
        CalculadorasDisponiveis.Clear();
        CalculadorasDisponiveis.Add("Todas as calculadoras");
        foreach (var tipo in tipos)
            CalculadorasDisponiveis.Add(tipo);
        if (!CalculadorasDisponiveis.Contains(CalculadoraSelecionada))
            CalculadoraSelecionada = "Todas as calculadoras";
        Filtrar();
    }

    private void Filtrar()
    {
        var filtro = Filtro.Trim();
        foreach (var selecionado in _itens.Where(item => item.Selecionado))
            selecionado.Selecionado = false;
        Itens.Clear();
        var encontrados = _itens.Where(item =>
            (filtro.Length == 0 || Contem(item.Nome, filtro) || Contem(item.Calculo.Calculadora, filtro))
            && (CalculadoraSelecionada == "Todas as calculadoras" || item.Calculo.Calculadora == CalculadoraSelecionada)
            && (PeriodoSelecionado switch
            {
                "Últimos 30 dias" => item.Calculo.AlteradoEm.Date >= DateTime.Today.AddDays(-30),
                "Este ano" => item.Calculo.AlteradoEm.Year == DateTime.Today.Year,
                _ => true
            }));
        encontrados = OrdemSelecionada switch
        {
            "Mais antigos" => encontrados.OrderBy(item => item.Calculo.AlteradoEm),
            "Nome" => encontrados.OrderBy(item => item.Nome, StringComparer.CurrentCultureIgnoreCase),
            _ => encontrados.OrderByDescending(item => item.Calculo.AlteradoEm)
        };
        foreach (var item in encontrados)
            Itens.Add(item);
        OnPropertyChanged(nameof(TemItens));
        OnPropertyChanged(nameof(MensagemVazia));
        OnPropertyChanged(nameof(Resumo));
        AtualizarComparacao();
    }

    private bool PodeComparar()
    {
        var selecionados = _itens.Where(item => item.Selecionado).Take(3).ToArray();
        return selecionados.Length == 2 && selecionados[0].Calculo.Tipo == selecionados[1].Calculo.Tipo;
    }

    private void AtualizarComparacao()
    {
        OnPropertyChanged(nameof(SituacaoComparacao));
        ((AsyncRelayCommand)CompararCommand).RaiseCanExecuteChanged();
    }

    private async Task CompararAsync()
    {
        var selecionados = _itens.Where(item => item.Selecionado).Select(item => item.Calculo).ToArray();
        if (selecionados.Length != 2 || selecionados[0].Tipo != selecionados[1].Tipo)
            return;
        try
        {
            var primeiro = await _historico.ObterAsync(selecionados[0].Id, CancellationToken.None);
            var segundo = await _historico.ObterAsync(selecionados[1].Id, CancellationToken.None);
            if (primeiro is null || segundo is null)
            {
                _notificador.MostrarAviso("Um dos cálculos selecionados não está mais no histórico. Atualize a lista.", "Histórico");
                return;
            }
            if (primeiro.Tipo != segundo.Tipo)
            {
                _notificador.MostrarAviso("Os cálculos selecionados não pertencem mais à mesma calculadora. Atualize a lista.", "Histórico");
                return;
            }
            _comparacao.Mostrar(primeiro, segundo);
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro("Não foi possível comparar os cálculos salvos.", exception);
        }
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
