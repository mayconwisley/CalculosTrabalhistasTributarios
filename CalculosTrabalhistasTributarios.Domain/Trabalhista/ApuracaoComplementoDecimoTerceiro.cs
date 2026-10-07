namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>13º pago em dezembro com a média usada na 2ª parcela e o 13º revisto com a média final do ano.</summary>
/// <param name="VariaveisAno">Soma das variáveis de janeiro a dezembro.</param>
/// <param name="MediaFinal">Variáveis do ano ÷ avos, arredondada aos centavos.</param>
public sealed record ApuracaoComplementoDecimoTerceiro(
    decimal Salario,
    decimal MediaPaga,
    decimal VariaveisAteNovembro,
    decimal VariaveisDezembro,
    decimal VariaveisAno,
    decimal MediaFinal,
    int Avos,
    decimal IntegralPago,
    decimal IntegralRevisado)
{
    /// <summary>Positiva, complemento a pagar; negativa, valor pago a maior que pode ser compensado.</summary>
    public decimal Diferenca => IntegralRevisado - IntegralPago;
}
