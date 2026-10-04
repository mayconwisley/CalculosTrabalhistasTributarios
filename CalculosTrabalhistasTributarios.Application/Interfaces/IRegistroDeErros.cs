namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>Guarda os detalhes de erros inesperados, para o usuário enviar ao suporte e o mantenedor investigar.</summary>
public interface IRegistroDeErros
{
    /// <summary>Arquivo em que os erros são registrados, exibido ao usuário.</summary>
    string Arquivo { get; }

    /// <summary>Registra o erro com o que estava sendo feito; nunca lança exceção, porque é chamado justamente quando algo deu errado.</summary>
    void Registrar(Exception exception, string contexto);
}
