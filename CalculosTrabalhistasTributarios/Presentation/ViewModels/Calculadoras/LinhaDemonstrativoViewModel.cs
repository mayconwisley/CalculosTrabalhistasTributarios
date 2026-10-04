namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <param name="Provento">Valor formatado na coluna de proventos; vazio nas linhas de desconto.</param>
public sealed record LinhaDemonstrativoViewModel(string Descricao, string Referencia, string Provento, string Desconto)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => string.Join(" • ", new[] { Descricao, Referencia, Provento, Desconto }.Where(texto => !string.IsNullOrWhiteSpace(texto)));
}
