using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly Func<Window> _criarJanela;
    private readonly IPreferenciasMobile _preferencias;
    private readonly IPlataformaMobile _plataforma;

    public App(Func<Window> criarJanela, IPreferenciasMobile preferencias, IPlataformaMobile plataforma)
    {
        InitializeComponent();
        _criarJanela = criarJanela;
        _preferencias = preferencias;
        _plataforma = plataforma;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _plataforma.AplicarTema(_preferencias.Tema);
        return _criarJanela();
    }
}
