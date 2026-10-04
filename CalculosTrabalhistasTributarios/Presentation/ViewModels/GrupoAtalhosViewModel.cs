namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record GrupoAtalhosViewModel(string Titulo, IReadOnlyList<AtalhoViewModel> Atalhos);
