namespace CalculosTrabalhistasTributarios.Mobile.Views;

/// <summary>
/// Logotipo do desktop na versão de cada tema. Definido em código: o AppThemeBinding de ImageSource escrito no XAML
/// fazia o compilador AOT do Android (mono-aot-cross 10.0.13) encerrar com corrupção de heap na compilação Release.
/// </summary>
public sealed class Logotipo : Image
{
    public Logotipo()
    {
        this.SetAppTheme<ImageSource>(SourceProperty, ImageSource.FromFile("logo_claro.png"), ImageSource.FromFile("logo_escuro.png"));
        SemanticProperties.SetDescription(this, "Logotipo de Cálculos Trabalhistas e Tributários");
    }
}
