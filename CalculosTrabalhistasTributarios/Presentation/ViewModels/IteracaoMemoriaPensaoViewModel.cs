namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record IteracaoMemoriaPensaoViewModel(string Titulo, IReadOnlyList<FormulaCalculoViewModel> Formulas)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => Titulo;
}
