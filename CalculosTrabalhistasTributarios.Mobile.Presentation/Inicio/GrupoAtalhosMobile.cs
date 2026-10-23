using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

/// <summary>Grupo de atalhos com os itens que atendem à busca e não estão nas favoritas.</summary>
public sealed partial class GrupoAtalhosMobile(string titulo, IReadOnlyList<AtalhoMobile> todos) : ObservableObject
{
    private static readonly IReadOnlyList<AtalhoMobile> Nenhum = [];
    private bool _emBusca;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItensMenu))]
    [NotifyPropertyChangedFor(nameof(IconeExpandir))]
    private bool _expandido;

    [ObservableProperty] private bool _visivel = true;

    public string Titulo { get; } = titulo;
    public IReadOnlyList<AtalhoMobile> Todos { get; } = todos;
    public ObservableCollection<AtalhoMobile> Atalhos { get; } = [.. todos];

    /// <summary>
    /// Itens do menu lateral: só os do grupo aberto ou, durante a busca, os encontrados. O grupo fechado não cria os
    /// controles dos itens, para o menu abrir rápido.
    /// </summary>
    public IReadOnlyList<AtalhoMobile> ItensMenu => Expandido || _emBusca ? Atalhos : Nenhum;
    public string IconeExpandir => Expandido || _emBusca ? "▾" : "▸";

    [RelayCommand]
    private void Alternar() => Expandido = !Expandido;

    internal void Atualizar(IEnumerable<AtalhoMobile> itens, bool emBusca)
    {
        var lista = itens.ToList();
        var mudouBusca = _emBusca != emBusca;
        _emBusca = emBusca;
        if (!Atalhos.SequenceEqual(lista))
        {
            Atalhos.Clear();
            foreach (var item in lista) Atalhos.Add(item);
        }
        Visivel = lista.Count > 0;
        if (mudouBusca)
        {
            OnPropertyChanged(nameof(ItensMenu));
            OnPropertyChanged(nameof(IconeExpandir));
        }
    }
}
