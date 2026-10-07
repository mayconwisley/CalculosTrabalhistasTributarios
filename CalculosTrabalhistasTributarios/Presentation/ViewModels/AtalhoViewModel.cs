using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Cartão da tela inicial que abre uma calculadora ou uma tabela e pode ser marcado como favorito.</summary>
/// <param name="Chave">Identificador estável do cartão, gravado nas favoritas; não muda se o título mudar.</param>
public sealed class AtalhoViewModel(string chave, string titulo, string descricao, ICommand abrir) : ViewModelBase
{
    private bool _favorito;

    public string Chave { get; } = chave;
    public string Titulo { get; } = titulo;
    public string Descricao { get; } = descricao;
    public ICommand Abrir { get; } = abrir;

    /// <summary>Grupo de origem, também usado na busca.</summary>
    public string Grupo { get; internal set; } = string.Empty;

    public ICommand? AlternarFavorito { get; internal set; }

    public bool Favorito
    {
        get => _favorito;
        internal set
        {
            if (SetProperty(ref _favorito, value))
                OnPropertyChanged(nameof(DescricaoFavorito));
        }
    }

    /// <summary>Nome acessível e dica da estrela, que dizem o que o clique vai fazer.</summary>
    public string DescricaoFavorito => Favorito ? $"Remover {Titulo} das favoritas" : $"Marcar {Titulo} como favorita";

    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => Titulo;
}
