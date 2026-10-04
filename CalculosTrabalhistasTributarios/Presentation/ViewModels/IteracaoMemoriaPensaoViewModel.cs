namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record IteracaoMemoriaPensaoViewModel(string Titulo, IReadOnlyList<FormulaCalculoViewModel> Formulas);
