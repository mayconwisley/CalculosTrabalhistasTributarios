namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record ComparativoIrrfViewModel(
    string Modalidade,
    string BaseCalculo,
    string ReducaoMensal,
    string ImpostoFinal,
    bool EhMaisVantajosa)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => $"{Modalidade}: base {BaseCalculo}, imposto {ImpostoFinal}{(EhMaisVantajosa ? ", mais vantajosa" : "")}";
}
