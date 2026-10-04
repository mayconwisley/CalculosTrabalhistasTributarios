namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record LinhaFaixaTributariaViewModel(string Faixa, string BaseCalculada, string Aliquota, string Imposto)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => $"Faixa {Faixa}: {BaseCalculada} x {Aliquota} = {Imposto}";
}
