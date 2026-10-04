namespace CalculosTrabalhistasTributarios.Domain.Atualizacao;

/// <param name="DiasDeAtraso">Dias corridos do dia seguinte ao vencimento até o pagamento.</param>
/// <param name="PrimeiroMesSelic">Primeiro mês com Selic nos juros; nulo quando o pagamento é no mês do vencimento ou no seguinte.</param>
/// <param name="SelicAcumulada">Soma das taxas mensais da Selic, em %.</param>
/// <param name="PercentualJuros">Selic acumulada mais 1% do mês do pagamento; zero quando o pagamento é no mês do vencimento.</param>
public sealed record AcrescimosDeMora(
    decimal Principal,
    int DiasDeAtraso,
    decimal PercentualMulta,
    decimal Multa,
    DateOnly? PrimeiroMesSelic,
    DateOnly? UltimoMesSelic,
    decimal SelicAcumulada,
    decimal PercentualJuros,
    decimal Juros)
{
    public decimal Total => Principal + Multa + Juros;
}
