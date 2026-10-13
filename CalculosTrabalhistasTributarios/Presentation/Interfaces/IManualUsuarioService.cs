namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface IManualUsuarioService
{
    /// <summary>Abre o manual; com a chave de uma janela, como "Calculadora.Rescisao", na seção dessa calculadora.</summary>
    void Abrir(string? chaveJanela = null);
}
