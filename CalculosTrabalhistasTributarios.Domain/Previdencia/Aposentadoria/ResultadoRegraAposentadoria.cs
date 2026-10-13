namespace CalculosTrabalhistasTributarios.Domain.Previdencia.Aposentadoria;

/// <summary>Uma regra de aposentadoria: se vale para o segurado, quando os requisitos se cumprem e o benefício estimado.</summary>
/// <param name="Motivo">Por que a regra não vale para o segurado; nulo quando vale.</param>
/// <param name="Data">Primeiro mês em que os requisitos se cumprem; nulo se não se cumprem no horizonte da projeção.</param>
/// <param name="Coeficiente">Percentual da média: 60% mais 2 pontos por ano excedente, ou 100% no pedágio de 100%; nulo no pedágio de 50%.</param>
/// <param name="Fator">Fator previdenciário do pedágio de 50%; nulo sem a expectativa de sobrevida ou nas demais regras.</param>
/// <param name="Valor">Benefício estimado, entre o salário mínimo e o teto; nulo quando não calculado.</param>
public sealed record ResultadoRegraAposentadoria(
    RegraAposentadoria Regra,
    string Requisitos,
    string? Motivo,
    DateOnly? Data = null,
    int IdadeMeses = 0,
    int TempoMeses = 0,
    decimal? Coeficiente = null,
    decimal? Fator = null,
    decimal? Valor = null)
{
    public bool Aplicavel => Motivo is null;
    public bool Alcancada => Aplicavel && Data is not null;
}
