namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record FormulaCalculoViewModel(string Titulo, string Formula)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => $"{Titulo}: {Formula}";
}
