using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>
/// Faixa no alto da tela principal, com a ação que resolve o aviso, um link opcional e o botão para dispensá-lo nesta
/// sessão. O texto muda durante a ação, como o progresso do download de uma versão nova.
/// </summary>
public sealed class AvisoInicioViewModel(string texto, string textoAcao, ICommand acao, ICommand dispensar, string? textoLink = null, ICommand? abrirLink = null) : ViewModelBase
{
    private string _texto = texto;

    public string Texto { get => _texto; set => SetProperty(ref _texto, value); }
    public string TextoAcao { get; } = textoAcao;
    public ICommand Acao { get; } = acao;
    public ICommand Dispensar { get; } = dispensar;
    public string? TextoLink { get; } = textoLink;
    public ICommand? AbrirLink { get; } = abrirLink;
    public bool TemLink => AbrirLink is not null;

    // A lista usa o texto do item como nome acessível: o leitor de tela lê o aviso, e não o nome do tipo.
    public override string ToString() => Texto;
}
