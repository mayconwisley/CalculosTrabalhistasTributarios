using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>
/// Cartões de uma aba da tela inicial (calculadoras ou tabelas): a seção de favoritas, na ordem em que foram marcadas, e
/// os grupos com os demais cartões. A busca procura no que o cartão mostra, o título e a descrição, sem diferenciar
/// maiúsculas e acentos; com várias palavras, o cartão precisa ter todas.
/// </summary>
public sealed class PainelAtalhosViewModel : ViewModelBase
{
    private static readonly CompareInfo Comparacao = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
    private readonly IFavoritosAtalhos _favoritos;
    private readonly Dictionary<string, AtalhoViewModel> _porChave;
    private string _filtro = string.Empty;
    private bool _temFavoritas;
    private bool _semResultado;

    public PainelAtalhosViewModel(IReadOnlyList<GrupoAtalhosViewModel> grupos, IFavoritosAtalhos favoritos, string nomeDosItens)
    {
        Grupos = grupos;
        NomeDosItens = nomeDosItens;
        _favoritos = favoritos;
        _porChave = grupos.SelectMany(grupo => grupo.Todos).ToDictionary(atalho => atalho.Chave);
        foreach (var atalho in _porChave.Values)
        {
            atalho.Favorito = favoritos.Chaves.Contains(atalho.Chave);
            atalho.AlternarFavorito = new RelayCommand(_ => Alternar(atalho));
        }
        Atualizar();
    }

    public IReadOnlyList<GrupoAtalhosViewModel> Grupos { get; }
    public ObservableCollection<AtalhoViewModel> Favoritas { get; } = [];

    /// <summary>"calculadora" ou "tabela", para as mensagens.</summary>
    public string NomeDosItens { get; }

    /// <summary>As favoritas usam as mesmas colunas dos grupos, para os cartões ficarem alinhados.</summary>
    public int Colunas => Grupos.Count;
    public int Quantidade => _porChave.Count;

    public bool TemFavoritas { get => _temFavoritas; private set => SetProperty(ref _temFavoritas, value); }
    public bool SemResultado { get => _semResultado; private set => SetProperty(ref _semResultado, value); }
    public string MensagemSemResultado => $"Nenhuma {NomeDosItens} tem “{Filtro.Trim()}” no nome ou na descrição.";

    public string Filtro
    {
        get => _filtro;
        set
        {
            if (!SetProperty(ref _filtro, value ?? string.Empty)) return;
            Atualizar();
            OnPropertyChanged(nameof(MensagemSemResultado));
        }
    }

    private void Alternar(AtalhoViewModel atalho)
    {
        atalho.Favorito = !atalho.Favorito;
        _favoritos.Definir(atalho.Chave, atalho.Favorito);
        Atualizar();
    }

    private void Atualizar()
    {
        var termos = Filtro.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool Atende(AtalhoViewModel atalho) => termos.All(termo => Contem(atalho.Titulo, termo) || Contem(atalho.Descricao, termo));

        // Chaves gravadas de cartões que não existem mais (por exemplo, de outra versão) são ignoradas.
        var favoritas = _favoritos.Chaves.Where(_porChave.ContainsKey).Select(chave => _porChave[chave]).Where(Atende).ToArray();
        Substituir(Favoritas, favoritas);
        foreach (var grupo in Grupos)
        {
            Substituir(grupo.Atalhos, grupo.Todos.Where(atalho => !atalho.Favorito && Atende(atalho)).ToArray());
            grupo.TodosFavoritos = termos.Length == 0 && grupo.Atalhos.Count == 0;
            grupo.SemResultado = termos.Length > 0 && grupo.Atalhos.Count == 0;
        }
        TemFavoritas = favoritas.Length > 0;
        SemResultado = termos.Length > 0 && !TemFavoritas && Grupos.All(grupo => grupo.Atalhos.Count == 0);
    }

    // Só mexe na coleção quando a lista muda, para a tela não redesenhar os cartões a cada tecla sem efeito.
    private static void Substituir(ObservableCollection<AtalhoViewModel> colecao, IReadOnlyList<AtalhoViewModel> itens)
    {
        if (colecao.SequenceEqual(itens)) return;
        colecao.Clear();
        foreach (var item in itens) colecao.Add(item);
    }

    private static bool Contem(string texto, string termo) => Comparacao.IndexOf(texto, termo, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
