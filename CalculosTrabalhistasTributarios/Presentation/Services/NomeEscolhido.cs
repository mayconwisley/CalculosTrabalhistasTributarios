namespace CalculosTrabalhistasTributarios.Presentation.Services;

/// <param name="ComoNovo">Salvar uma cópia nova em vez de substituir o cálculo aberto do histórico.</param>
public sealed record NomeEscolhido(string Nome, bool ComoNovo);
