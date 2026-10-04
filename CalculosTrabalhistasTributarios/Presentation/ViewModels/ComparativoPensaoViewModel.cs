namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record ComparativoPensaoViewModel(string Modalidade, string IrrfFinal, string Pensao, string Total)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => $"{Modalidade}: IRRF {IrrfFinal}, pensão {Pensao}, total {Total}";
}
