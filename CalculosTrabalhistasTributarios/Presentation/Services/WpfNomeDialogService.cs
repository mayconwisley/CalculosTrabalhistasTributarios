using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Views;
using System.Windows;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

public sealed class WpfNomeDialogService : INomeDialogService
{
    public NomeEscolhido? SolicitarNome(string titulo, string acao, string nomeAtual, bool oferecerComoNovo = false)
    {
        var aplicativo = System.Windows.Application.Current;
        var janela = new NomeCalculoWindow(titulo, acao, nomeAtual, oferecerComoNovo)
        {
            Owner = aplicativo.Windows.OfType<Window>().FirstOrDefault(janela => janela.IsActive) ?? aplicativo.MainWindow
        };
        return janela.ShowDialog() == true ? new NomeEscolhido(janela.NomeInformado, janela.ComoNovo) : null;
    }
}
