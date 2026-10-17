namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <param name="Quadro">As faixas do INSS ou do IRRF em tabela, abaixo das fórmulas; nulo nos demais grupos.</param>
public sealed record SecaoMemoriaIrrfViewModel(string Titulo, string Destaque, IReadOnlyList<FormulaCalculoViewModel> Formulas, QuadroFaixasViewModel? Quadro = null)
{
    public bool TemQuadro => Quadro is not null;

    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => $"{Titulo}: {Destaque}";
}
