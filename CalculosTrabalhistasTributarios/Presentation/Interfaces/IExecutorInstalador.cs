namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

/// <summary>Executa o instalador de uma versão nova e fecha o aplicativo, para os arquivos poderem ser substituídos.</summary>
public interface IExecutorInstalador
{
    void InstalarEFechar(string instalador);
}
