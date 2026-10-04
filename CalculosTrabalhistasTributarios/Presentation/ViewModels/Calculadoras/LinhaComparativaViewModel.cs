namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed record LinhaComparativaViewModel(string Descricao, IReadOnlyList<string> Valores, bool Destaque)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => $"{Descricao}: {string.Join(", ", Valores)}";
}
