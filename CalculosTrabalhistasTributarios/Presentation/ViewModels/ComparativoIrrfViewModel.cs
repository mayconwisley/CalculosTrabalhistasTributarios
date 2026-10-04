namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed record ComparativoIrrfViewModel(
    string Modalidade,
    string BaseCalculo,
    string ReducaoMensal,
    string ImpostoFinal,
    bool EhMaisVantajosa);
