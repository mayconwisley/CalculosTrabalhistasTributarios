namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Pensão de um beneficiário no resultado, quando há mais de um.</summary>
public sealed record PensaoPorBeneficiarioViewModel(string Nome, string Regra, string Pensao)
{
    // As listas usam o texto do item como nome acessível: o leitor de tela lê o conteúdo, e não o nome do tipo.
    public override string ToString() => $"{Nome}: {Regra}, {Pensao}";
}
