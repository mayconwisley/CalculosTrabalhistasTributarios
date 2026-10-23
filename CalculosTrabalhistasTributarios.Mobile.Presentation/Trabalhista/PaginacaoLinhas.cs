using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>Parte da página de linhas usada pelo controle de navegação, que não depende do tipo da linha.</summary>
public abstract class PaginacaoLinhas : ObservableObject
{
    public abstract IRelayCommand AnteriorCommand { get; }
    public abstract IRelayCommand ProximaCommand { get; }
    public abstract bool TemPaginas { get; }
    /// <summary>Faixa exibida, como "13–24 de 60".</summary>
    public abstract string Texto { get; }
}
