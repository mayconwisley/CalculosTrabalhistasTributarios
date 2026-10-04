using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Faixa no alto da tela principal, com a ação que resolve o aviso e o botão para dispensá-lo nesta sessão.</summary>
public sealed record AvisoInicioViewModel(string Texto, string TextoAcao, ICommand Acao, ICommand Dispensar)
{
    // A lista usa o texto do item como nome acessível: o leitor de tela lê o aviso, e não o nome do tipo.
    public override string ToString() => Texto;
}
