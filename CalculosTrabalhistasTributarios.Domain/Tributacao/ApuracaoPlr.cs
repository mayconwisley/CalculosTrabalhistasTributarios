namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <param name="Tabela">Faixas da tabela anual usada, da menor para a maior, para a memória mostrar os limites.</param>
/// <param name="DetalhesProgressivos">
/// Base e imposto exato de cada faixa, sem arredondar, para conferir o imposto calculado pela parcela a deduzir.
/// </param>
public sealed record ApuracaoPlr(
    decimal BaseCalculo,
    int Faixa,
    decimal Aliquota,
    decimal Deducao,
    decimal Imposto,
    IReadOnlyList<FaixaTributaria>? Tabela = null,
    IReadOnlyList<ResultadoFaixaTributaria>? DetalhesProgressivos = null);
