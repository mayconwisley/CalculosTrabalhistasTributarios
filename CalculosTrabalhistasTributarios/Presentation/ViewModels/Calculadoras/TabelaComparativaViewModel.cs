namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Uma tabela de comparação lado a lado do resultado, como a do ano e a do mês.</summary>
public sealed record TabelaComparativaViewModel(string Titulo, IReadOnlyList<string> Colunas, IReadOnlyList<LinhaComparativaViewModel> Linhas);
