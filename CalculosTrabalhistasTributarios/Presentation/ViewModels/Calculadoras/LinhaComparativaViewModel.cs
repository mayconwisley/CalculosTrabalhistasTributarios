namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed record LinhaComparativaViewModel(string Descricao, IReadOnlyList<string> Valores, bool Destaque);
