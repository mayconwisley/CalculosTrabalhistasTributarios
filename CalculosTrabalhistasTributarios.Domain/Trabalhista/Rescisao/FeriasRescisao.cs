namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

/// <summary>As férias vencidas e proporcionais da rescisão, com o terço constitucional.</summary>
/// <param name="EmDobro">O período vencido mais antigo, quando são dois: já passou do prazo de concessão (CLT, art. 137).</param>
/// <param name="InicioPeriodo">Início do período aquisitivo em curso.</param>
/// <param name="DiasDireito">Dias de férias pelas faltas do período (CLT, art. 130).</param>
/// <param name="Total">As proporcionais com a projeção do aviso; arredondadas uma vez.</param>
/// <param name="SobreAviso">A diferença entre o total e as proporcionais até o desligamento.</param>
public sealed record FeriasRescisao(
    decimal Vencidas,
    decimal EmDobro,
    decimal TercoVencidas,
    DateOnly InicioPeriodo,
    int DiasDireito,
    int Avos,
    int AvosAviso,
    decimal Total,
    decimal Proporcionais,
    decimal SobreAviso,
    decimal TercoProporcionais)
{
    /// <summary>Todas as férias da rescisão com 1/3.</summary>
    public decimal TotalComTerco => Total + TercoProporcionais + Vencidas + EmDobro + TercoVencidas;
}
