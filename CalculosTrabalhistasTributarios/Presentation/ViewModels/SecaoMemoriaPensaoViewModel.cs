namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record SecaoMemoriaPensaoViewModel(string Titulo, string Destaque, IReadOnlyList<IteracaoMemoriaPensaoViewModel> Iteracoes)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => $"{Titulo}: {Destaque}";
}
