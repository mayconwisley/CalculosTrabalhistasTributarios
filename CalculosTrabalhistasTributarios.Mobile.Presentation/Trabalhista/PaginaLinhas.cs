using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>
/// Página das linhas exibidas de uma lista que pode chegar a centenas de itens. As linhas ficam na página para que a rolagem
/// não prenda dentro de uma lista própria; exibir todas criaria milhares de controles e travaria a tela ao gerar um período
/// longo. O cálculo continua lendo a lista completa.
/// </summary>
public sealed class PaginaLinhas<T> : PaginacaoLinhas where T : class
{
    public const int TamanhoPadrao = 12;
    private readonly ObservableCollection<T> _origem;
    private readonly RelayCommand _anterior;
    private readonly RelayCommand _proxima;
    private int _inicio;

    public PaginaLinhas(ObservableCollection<T> origem, int tamanho = TamanhoPadrao)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tamanho, 1);
        _origem = origem;
        Tamanho = tamanho;
        _anterior = new RelayCommand(() => IrPara(_inicio - Tamanho), () => _inicio > 0);
        _proxima = new RelayCommand(() => IrPara(_inicio + Tamanho), () => _inicio + Tamanho < _origem.Count);
        origem.CollectionChanged += (_, _) => Atualizar();
        Atualizar();
    }

    public int Tamanho { get; }
    public ObservableCollection<T> Exibidas { get; } = [];
    public override IRelayCommand AnteriorCommand => _anterior;
    public override IRelayCommand ProximaCommand => _proxima;
    public override bool TemPaginas => _origem.Count > Tamanho;
    public override string Texto => _origem.Count == 0 ? string.Empty
        : Math.Min(_inicio + Tamanho, _origem.Count) is var fim && fim == _inicio + 1 ? $"{fim} de {_origem.Count}"
        : $"{_inicio + 1}–{fim} de {_origem.Count}";

    /// <summary>Mostra a página da linha, como a recém-adicionada ou a que tem um erro de preenchimento.</summary>
    public void Exibir(T linha)
    {
        var indice = _origem.IndexOf(linha);
        if (indice >= 0) IrPara(indice / Tamanho * Tamanho);
    }

    private void IrPara(int inicio)
    {
        _inicio = inicio;
        Atualizar();
    }

    private void Atualizar()
    {
        _inicio = Math.Clamp(_inicio, 0, Math.Max(0, (_origem.Count - 1) / Tamanho * Tamanho));
        var alvo = _origem.Skip(_inicio).Take(Tamanho).ToList();
        // Sincroniza em vez de recriar: linhas que continuam na página mantêm os controles e o foco.
        for (var i = Exibidas.Count - 1; i >= 0; i--)
            if (!alvo.Contains(Exibidas[i])) Exibidas.RemoveAt(i);
        for (var i = 0; i < alvo.Count; i++)
        {
            if (i < Exibidas.Count && ReferenceEquals(Exibidas[i], alvo[i])) continue;
            var atual = Exibidas.IndexOf(alvo[i]);
            if (atual > i) Exibidas.Move(atual, i);
            else Exibidas.Insert(i, alvo[i]);
        }
        OnPropertyChanged(nameof(TemPaginas));
        OnPropertyChanged(nameof(Texto));
        _anterior.NotifyCanExecuteChanged();
        _proxima.NotifyCanExecuteChanged();
    }
}
