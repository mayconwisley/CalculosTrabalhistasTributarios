namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <param name="BaseInformada">Base antes da limitação ao teto.</param>
/// <param name="Tabela">
/// Faixas da tabela usada, da menor para a maior, para a memória mostrar os limites de cada faixa; nula quando o INSS não
/// segue a tabela do empregado, como os 11% do contribuinte individual.
/// </param>
/// <param name="Progressivo">Cada parte da base paga a alíquota da sua faixa (desde 03/2020); falso na alíquota única.</param>
public sealed record ApuracaoInss(decimal BaseInformada, decimal BaseConsiderada, decimal Valor, IReadOnlyList<ResultadoFaixaTributaria> Detalhes,
    IReadOnlyList<FaixaTributaria>? Tabela = null, bool Progressivo = true)
{
    public bool LimitadaAoTeto => BaseConsiderada < BaseInformada;
}
