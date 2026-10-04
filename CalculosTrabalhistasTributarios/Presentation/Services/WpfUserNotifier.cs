using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

public sealed class WpfUserNotifier : IUserNotifier
{
    public void MostrarAviso(string mensagem, string titulo = "Dados inválidos") =>
        MessageBox.Show(mensagem, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void MostrarErro(string mensagem, Exception exception) =>
        MessageBox.Show($"{mensagem}\n\n{MensagemUsuario.De(exception)}", "Cálculos Trabalhistas e Tributários", MessageBoxButton.OK, MessageBoxImage.Error);

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
