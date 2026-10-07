namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Parte do ano do pagamento, tributada no mês do recebimento e sujeita ao ajuste anual (Lei 7.713/1988, art. 12-B).</summary>
/// <param name="ComOutros">IRRF do mês com esta parte e os rendimentos normais da mesma fonte; nulo na Justiça Federal.</param>
/// <param name="SoOutros">IRRF dos rendimentos normais do mês, sem o RRA; nulo sem esses rendimentos ou na Justiça Federal.</param>
/// <param name="Imposto">IRRF atribuído a esta parte: a diferença entre os dois cálculos, ou os 3% da Justiça Federal.</param>
public sealed record ParteAnoPagamentoRra(
    decimal Parcelas,
    decimal Correcao,
    decimal Despesas,
    decimal Inss,
    decimal Pensao,
    ApuracaoIrrf? ComOutros,
    ApuracaoIrrf? SoOutros,
    decimal Imposto)
{
    public decimal Rendimentos => Parcelas + Correcao;
}
