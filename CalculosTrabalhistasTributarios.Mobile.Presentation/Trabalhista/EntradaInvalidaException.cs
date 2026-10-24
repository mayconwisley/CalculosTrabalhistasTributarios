namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

internal sealed class EntradaInvalidaException(string mensagem, CampoMobile? campo = null) : Exception(mensagem)
{
    public CampoMobile? Campo { get; } = campo;
}
