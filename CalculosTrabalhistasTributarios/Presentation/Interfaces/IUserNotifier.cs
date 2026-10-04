using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface IUserNotifier
{
    void MostrarAviso(string mensagem, string titulo = "Dados inválidos");
    void MostrarErro(string mensagem, Exception exception);

    /// <summary>
    /// Mostra uma falha esperada: o dado a corrigir como aviso; a tabela ausente ou a fonte fora do ar com o
    /// <paramref name="contexto"/>, que diz o que não pôde ser feito.
    /// </summary>
    void MostrarFalha(Erro erro, string contexto);

    /// <summary>Pergunta antes de uma ação que não pode ser desfeita; verdadeiro quando o usuário confirma.</summary>
    bool Confirmar(string mensagem, string titulo);
}
