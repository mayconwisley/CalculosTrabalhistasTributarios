namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

public sealed record ResultadoFaixaTributaria(
    int Faixa,
    decimal BaseCalculada,
    decimal Aliquota,
    decimal Imposto);
