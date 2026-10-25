using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>
/// A remoção de uma linha é imediata; esta é a última remoção da lista, que o usuário pode desfazer até remover outra linha.
/// A linha volta com os mesmos valores e na mesma posição.
/// </summary>
public sealed partial class DesfazerRemocaoMobile : ObservableObject
{
    private Func<string?>? _restaurar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visivel))]
    private string? _texto;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DesfazerCommand))]
    private bool _podeDesfazer;

    public bool Visivel => Texto is not null;

    /// <summary>Remove o item e guarda a posição para desfazer; <paramref name="limite"/> impede voltar além do máximo de linhas.</summary>
    public void Remover<T>(ObservableCollection<T> lista, T item, string descricao, int limite)
    {
        var indice = lista.IndexOf(item);
        if (indice < 0) return;
        lista.RemoveAt(indice);
        _restaurar = () =>
        {
            if (lista.Count >= limite) return $"Não foi possível desfazer: a lista já tem o limite de {limite} linhas.";
            lista.Insert(Math.Min(indice, lista.Count), item);
            return null;
        };
        Texto = $"Linha removida: {descricao}.";
        PodeDesfazer = true;
    }

    /// <summary>Esquece a remoção, por exemplo quando a lista inteira é substituída por um histórico ou um exemplo.</summary>
    public void Descartar()
    {
        _restaurar = null;
        Texto = null;
        PodeDesfazer = false;
    }

    [RelayCommand(CanExecute = nameof(PodeDesfazer))]
    private void Desfazer()
    {
        var restaurar = _restaurar;
        Descartar();
        Texto = restaurar?.Invoke();
    }
}
