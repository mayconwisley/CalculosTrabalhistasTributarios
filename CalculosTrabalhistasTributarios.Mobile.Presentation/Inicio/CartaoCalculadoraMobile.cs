namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

/// <param name="ViewModel">Formulário da tela comum de cálculo; nulo para as calculadoras com página própria.</param>
public sealed record CartaoCalculadoraMobile(TipoCalculadoraMobile Tipo, string Titulo, string Descricao, string TextoBotao, Type? ViewModel)
{
    public string IdAutomacao => $"Abrir{Tipo}";
}
