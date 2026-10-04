using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using System.Windows;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

/// <summary>Mensagens em caixas de diálogo; os erros inesperados também vão para o registro, para o suporte.</summary>
public sealed class WpfUserNotifier(IRegistroDeErros registroDeErros) : IUserNotifier
{
    public void MostrarAviso(string mensagem, string titulo = "Dados inválidos") =>
        MessageBox.Show(mensagem, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void MostrarErro(string mensagem, Exception exception)
    {
        registroDeErros.Registrar(exception, mensagem);
        MessageBox.Show($"{mensagem}\n\n{MensagemUsuario.De(exception)}\n\nOs detalhes foram registrados em:\n{registroDeErros.Arquivo}",
            "Cálculos Trabalhistas e Tributários", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void MostrarFalha(Erro erro, string contexto)
    {
        if (erro.Tipo == TipoErro.Validacao)
            MostrarAviso(erro.Mensagem);
        else
            MessageBox.Show($"{contexto}\n\n{erro.Mensagem}", "Cálculos Trabalhistas e Tributários", MessageBoxButton.OK,
                erro.Tipo == TipoErro.Indisponivel ? MessageBoxImage.Error : MessageBoxImage.Warning);
    }

    public bool Confirmar(string mensagem, string titulo) =>
        MessageBox.Show(mensagem, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
}
