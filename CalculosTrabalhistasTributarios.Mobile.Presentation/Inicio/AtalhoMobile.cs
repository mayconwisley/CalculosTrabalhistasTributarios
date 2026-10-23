using CommunityToolkit.Mvvm.ComponentModel;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

/// <summary>Calculadora ou tabela do menu e da tela inicial, que pode ser marcada como favorita.</summary>
public sealed partial class AtalhoMobile(string chave, string titulo, string descricao, string textoBotao, Func<Task> abrir) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconeFavorito))]
    [NotifyPropertyChangedFor(nameof(DescricaoFavorito))]
    private bool _favorito;

    /// <summary>"Calculadora.Tipo" ou "Tabela.Tipo", como as chaves de favoritos do desktop.</summary>
    public string Chave { get; } = chave;
    public string Titulo { get; } = titulo;
    public string Descricao { get; } = descricao;
    public string TextoBotao { get; } = textoBotao;
    public string IdAutomacao => $"Abrir{Chave.Replace(".", string.Empty, StringComparison.Ordinal)}";
    public string IconeFavorito => Favorito ? "★" : "☆";
    public string DescricaoFavorito => Favorito ? $"Remover {Titulo} das favoritas" : $"Marcar {Titulo} como favorita";

    internal Task AbrirAsync() => abrir();
}
