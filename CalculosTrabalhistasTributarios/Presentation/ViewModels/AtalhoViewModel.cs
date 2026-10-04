using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Cartão da tela inicial que abre uma calculadora ou uma tabela.</summary>
public sealed record AtalhoViewModel(string Titulo, string Descricao, ICommand Abrir)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => Titulo;
}
