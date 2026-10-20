using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Apresenta no menu uma categoria formada por cartões já filtrados da central.</summary>
public sealed class GrupoMenuLateralViewModel : ViewModelBase
{
    private readonly Func<IEnumerable<AtalhoViewModel>> _itens;
    // Os grupos começam recolhidos, para o menu caber inteiro na tela; as favoritas são abertas por quem as cria.
    private bool _expandido;
    private bool? _expandidoAntesDaBusca;

    /// <summary>Grupo com os cartões visíveis de <paramref name="origens"/>, na ordem de <paramref name="ordem"/>.</summary>
    public GrupoMenuLateralViewModel(string titulo, string icone, IReadOnlyList<AtalhoViewModel> ordem, params GrupoAtalhosViewModel[] origens)
        : this(titulo, icone, () =>
        {
            var visiveis = origens.SelectMany(origem => origem.Atalhos).ToHashSet();
            return ordem.Where(visiveis.Contains);
        }, [.. origens.Select(origem => origem.Atalhos)])
    {
    }

    /// <summary>Grupo cujos itens vêm de <paramref name="itens"/>, refeito sempre que uma das listas observadas muda.</summary>
    public GrupoMenuLateralViewModel(string titulo, string icone, Func<IEnumerable<AtalhoViewModel>> itens, params INotifyCollectionChanged[] observadas)
    {
        Titulo = titulo;
        Icone = icone;
        _itens = itens;
        foreach (var observada in observadas)
            observada.CollectionChanged += AtualizarAoFiltrar;
        Atualizar();
    }

    public string Titulo { get; }
    public string Icone { get; }
    public ObservableCollection<AtalhoViewModel> Atalhos { get; } = [];
    public bool Expandido { get => _expandido; set => SetProperty(ref _expandido, value); }

    /// <summary>
    /// Durante a busca, o grupo fica aberto para mostrar os itens encontrados; ao limpá-la,
    /// volta ao estado escolhido antes de começar a digitar.
    /// </summary>
    public void DefinirBusca(bool ativa)
    {
        if (ativa && _expandidoAntesDaBusca is null)
        {
            _expandidoAntesDaBusca = Expandido;
            Expandido = true;
        }
        else if (!ativa && _expandidoAntesDaBusca is bool anterior)
        {
            Expandido = anterior;
            _expandidoAntesDaBusca = null;
        }
        foreach (var subgrupo in Subgrupos)
            subgrupo.DefinirBusca(ativa);
    }

    /// <summary>Abre o grupo e a divisão que contêm o item aberto, para que o destaque fique à vista.</summary>
    public void Revelar(AtalhoViewModel atalho)
    {
        if (!Atalhos.Contains(atalho)) return;
        Expandido = true;
        foreach (var subgrupo in Subgrupos)
            subgrupo.Revelar(atalho);
    }

    /// <summary>Divisões do grupo, como as categorias das tabelas; com elas, o menu mostra os itens dentro de cada uma.</summary>
    public IReadOnlyList<GrupoMenuLateralViewModel> Subgrupos { get; init; } = [];
    public bool TemSubgrupos => Subgrupos.Count > 0;

    private void AtualizarAoFiltrar(object? sender, NotifyCollectionChangedEventArgs e) => Atualizar();

    private void Atualizar()
    {
        var itens = _itens().ToArray();
        if (Atalhos.SequenceEqual(itens)) return;
        Atalhos.Clear();
        foreach (var item in itens)
            Atalhos.Add(item);
    }
}
