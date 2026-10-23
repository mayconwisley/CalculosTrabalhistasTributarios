namespace CalculosTrabalhistasTributarios.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly Func<Window> _criarJanela;

    public App(Func<Window> criarJanela)
    {
        InitializeComponent();
        _criarJanela = criarJanela;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        _criarJanela();
}
