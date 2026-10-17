namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record LinhaQuadroFaixasViewModel(string Faixa, string De, string Ate, string ParteDaBase, string Aliquota, string ValorExato, string Valor)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() =>
        $"Faixa {Faixa}, de {De} até {Ate}: parte da base {ParteDaBase}, alíquota {Aliquota}, valor exato {ValorExato}, valor considerado {Valor}";
}
