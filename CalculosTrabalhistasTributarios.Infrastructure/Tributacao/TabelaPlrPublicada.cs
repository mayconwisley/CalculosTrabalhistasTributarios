using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>Tabela anual exclusiva da PLR, com o mesmo formato de faixas da tabela progressiva do IRRF.</summary>
public sealed record TabelaPlrPublicada(DateOnly Competencia, IReadOnlyList<FaixaIrrfPublicada> Faixas) : ITabelaPublicada<TabelaPlrPublicada>
{
    public void Validar() => SequenciaDecimal.ValidarFaixasProgressivas(Faixas, "da PLR");

    public bool TemMesmosValores(TabelaPlrPublicada outra) => Faixas.SequenceEqual(outra.Faixas);
}
