namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>IRRF do RRA separado por parte, com os juros não tributáveis e o total recebido.</summary>
/// <param name="DespesasNaoDedutiveis">Parte das despesas judiciais proporcional aos juros não tributáveis.</param>
public sealed record ApuracaoRra(
    ParteAnosAnterioresRra? AnosAnteriores,
    ParteAnoPagamentoRra? AnoPagamento,
    decimal JurosDeMora,
    decimal DespesasNaoDedutiveis,
    decimal TotalRecebido)
{
    public decimal Imposto => (AnosAnteriores?.Irrf.Imposto ?? 0m) + (AnoPagamento?.Imposto ?? 0m);
}
