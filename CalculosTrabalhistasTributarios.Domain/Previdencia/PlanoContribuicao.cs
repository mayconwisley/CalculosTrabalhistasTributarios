namespace CalculosTrabalhistasTributarios.Domain.Previdencia;

/// <summary>
/// Plano de contribuição (Lei 8.212/1991, art. 21): normal, de 20% sobre o salário de contribuição; simplificado, de 11%
/// sobre o salário mínimo; ou de baixa renda, de 5% sobre o salário mínimo, só para o facultativo.
/// </summary>
public enum PlanoContribuicao { Normal, Simplificado, BaixaRenda }
