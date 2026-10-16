namespace CalculosTrabalhistasTributarios.Domain.Previdencia;

/// <summary>Os salários corrigidos de cada competência e a média deles.</summary>
/// <param name="CorrigidoAte">Último mês de INPC aplicado; nulo quando nenhum salário precisou de correção.</param>
/// <param name="FaltaInpcDesde">Primeiro dos meses finais, até o anterior ao da simulação, ainda sem INPC cadastrado; nulo se a correção está completa.</param>
public sealed record ApuracaoMediaSalarios(
    IReadOnlyList<PeriodoSalarioContribuicao> Periodos,
    IReadOnlyList<SalarioCorrigido> Meses,
    DateOnly? CorrigidoAte,
    DateOnly? FaltaInpcDesde,
    decimal Soma,
    decimal Media)
{
    public int Quantidade => Meses.Count;
}
