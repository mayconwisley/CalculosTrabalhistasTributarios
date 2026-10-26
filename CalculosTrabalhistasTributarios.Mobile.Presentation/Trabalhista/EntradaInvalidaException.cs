namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>Falha de preenchimento; a orientação curta, quando há, aparece abaixo do campo e a mensagem completa no aviso.</summary>
internal sealed class EntradaInvalidaException(string mensagem, CampoMobile? campo = null, string? orientacao = null) : Exception(mensagem)
{
    public CampoMobile? Campo { get; } = campo;
    public string? Orientacao { get; } = orientacao;
}
