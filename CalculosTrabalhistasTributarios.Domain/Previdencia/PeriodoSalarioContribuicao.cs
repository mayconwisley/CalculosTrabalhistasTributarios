namespace CalculosTrabalhistasTributarios.Domain.Previdencia;

/// <summary>Competências seguidas com o mesmo salário de contribuição, como no CNIS; um só mês quando início e fim coincidem.</summary>
/// <param name="Inicio">Primeira competência, no primeiro dia do mês.</param>
/// <param name="Fim">Última competência, no primeiro dia do mês.</param>
/// <param name="Salario">Salário de contribuição de cada mês, já somados os vínculos do mesmo mês.</param>
public sealed record PeriodoSalarioContribuicao(DateOnly Inicio, DateOnly Fim, decimal Salario);
