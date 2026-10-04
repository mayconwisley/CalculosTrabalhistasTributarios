namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record IndicadorResumoViewModel(string Rotulo, string Valor, string? Complemento = null)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => string.Join(" • ", new[] { Rotulo, Valor, Complemento }.Where(texto => !string.IsNullOrWhiteSpace(texto)));
}
