namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record SecaoMemoriaPensaoViewModel(string Titulo, string Destaque, IReadOnlyList<IteracaoMemoriaPensaoViewModel> Iteracoes);
