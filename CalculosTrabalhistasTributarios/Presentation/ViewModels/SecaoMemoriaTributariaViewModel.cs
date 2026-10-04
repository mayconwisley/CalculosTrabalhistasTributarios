namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record SecaoMemoriaTributariaViewModel(string Titulo, string Total, IReadOnlyList<LinhaFaixaTributariaViewModel> Linhas);
