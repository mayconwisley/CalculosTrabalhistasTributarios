using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <param name="ValorDependente">Nulo quando a fonte não publica o valor; o valor já cadastrado continua valendo.</param>
/// <param name="ValorSimplificado">Nulo quando a fonte não publica o valor.</param>
/// <param name="ReducoesMensais">Nulo quando a fonte não publica a redução; a regra já cadastrada continua valendo.</param>
public sealed record TabelaIrrfPublicada(
    DateOnly Competencia,
    IReadOnlyList<FaixaIrrfPublicada> Faixas,
    decimal? ValorDependente = null,
    decimal? ValorSimplificado = null,
    IReadOnlyList<ReducaoMensalPublicada>? ReducoesMensais = null) : ITabelaPublicada<TabelaIrrfPublicada>
{
    /// <summary>Limite gravado na última faixa para representar "acima de".</summary>
    public const decimal LimiteUltimaFaixa = 9_999_999_999_999.99m;

    public void Validar() => SequenciaDecimal.ValidarFaixasProgressivas(Faixas, "IRRF");

    // Só as faixas: é o que todas as fontes publicam.
    public bool TemMesmosValores(TabelaIrrfPublicada outra) => Faixas.SequenceEqual(outra.Faixas);
}
