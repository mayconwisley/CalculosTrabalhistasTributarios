using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Views;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

/// <summary>Mensagens na caixa do aplicativo, sem o som do sistema; os erros inesperados também vão para o registro, para o suporte.</summary>
public sealed class WpfUserNotifier(IRegistroDeErros registroDeErros) : IUserNotifier
{
    public void MostrarAviso(string mensagem, string titulo = "Dados inválidos") =>
        CaixaMensagemWindow.Mostrar(mensagem, titulo, TipoCaixaMensagem.Aviso);

    public void MostrarErro(string mensagem, Exception exception)
    {
        registroDeErros.Registrar(exception, mensagem);
        CaixaMensagemWindow.Mostrar($"{mensagem}\n\n{MensagemUsuario.De(exception)}\n\nOs detalhes foram registrados em:\n{registroDeErros.Arquivo}",
            "Cálculos Trabalhistas e Tributários", TipoCaixaMensagem.Erro);
    }

    public void MostrarFalha(Erro erro, string contexto)
    {
        if (erro.Tipo == TipoErro.Validacao)
            MostrarAviso(erro.Mensagem);
        else
            CaixaMensagemWindow.Mostrar($"{contexto}\n\n{erro.Mensagem}", "Cálculos Trabalhistas e Tributários",
                erro.Tipo == TipoErro.Indisponivel ? TipoCaixaMensagem.Erro : TipoCaixaMensagem.Aviso);
    }

    public bool Confirmar(string mensagem, string titulo) =>
        CaixaMensagemWindow.Mostrar(mensagem, titulo, TipoCaixaMensagem.Pergunta);
}
