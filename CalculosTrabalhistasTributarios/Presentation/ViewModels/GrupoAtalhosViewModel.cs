using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Collections.ObjectModel;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>
/// Coluna de cartões da tela inicial. Mostra os que atendem à busca e não estão nas favoritas; a coluna fica na tela
/// mesmo vazia, para as demais não mudarem de largura nem se desalinharem das favoritas.
/// </summary>
public sealed class GrupoAtalhosViewModel : ViewModelBase
{
    private bool _todosFavoritos;
    private bool _semResultado;

    public GrupoAtalhosViewModel(string titulo, IReadOnlyList<AtalhoViewModel> todos)
    {
        Titulo = titulo;
        Todos = todos;
        foreach (var atalho in todos)
        {
            atalho.Grupo = titulo;
            Atalhos.Add(atalho);
        }
    }

    public string Titulo { get; }
    public IReadOnlyList<AtalhoViewModel> Todos { get; }
    public ObservableCollection<AtalhoViewModel> Atalhos { get; } = [];

    /// <summary>Sem busca, todos os cartões do grupo estão nas favoritas.</summary>
    public bool TodosFavoritos { get => _todosFavoritos; internal set => SetProperty(ref _todosFavoritos, value); }

    /// <summary>Com busca, nenhum cartão do grupo atende.</summary>
    public bool SemResultado { get => _semResultado; internal set => SetProperty(ref _semResultado, value); }

    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => Titulo;
}
