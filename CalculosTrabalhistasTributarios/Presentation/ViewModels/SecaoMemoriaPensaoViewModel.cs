namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <param name="Quadro">Faixas do IRRF na base da última iteração; nulo sem base de cálculo.</param>
/// <param name="ConferenciaDoQuadro">A soma das faixas comparada ao IR progressivo pela parcela a deduzir.</param>
public sealed record SecaoMemoriaPensaoViewModel(string Titulo, string Destaque, IReadOnlyList<IteracaoMemoriaPensaoViewModel> Iteracoes, QuadroFaixasViewModel? Quadro = null,
    string ConferenciaDoQuadro = "")
{
    public bool TemQuadro => Quadro is not null;

    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => $"{Titulo}: {Destaque}";
}
