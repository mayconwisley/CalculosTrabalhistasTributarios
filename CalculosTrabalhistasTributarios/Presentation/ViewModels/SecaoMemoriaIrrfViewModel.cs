namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record SecaoMemoriaIrrfViewModel(string Titulo, string Destaque, IReadOnlyList<FormulaCalculoViewModel> Formulas);
