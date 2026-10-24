using CommunityToolkit.Mvvm.ComponentModel;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed partial class SecaoCamposMobile(string titulo, IReadOnlyList<CampoMobile> campos) : ObservableObject
{
    public string Titulo { get; } = titulo;
    public IReadOnlyList<CampoMobile> Campos { get; } = campos;
    [ObservableProperty] private bool _expandida = true;
}
