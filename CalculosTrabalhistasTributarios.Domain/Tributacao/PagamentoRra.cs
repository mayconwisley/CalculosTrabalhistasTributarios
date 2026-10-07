namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Pagamento de rendimentos recebidos acumuladamente e as deduções informadas para cada parte.</summary>
/// <param name="CorrecaoMonetaria">Atualização tributável paga sobre as parcelas, rateada pelo valor delas.</param>
/// <param name="JurosDeMora">Juros de mora pelo atraso da remuneração, não tributáveis (IN RFB 1.500/2014, art. 36, § 4º).</param>
/// <param name="DespesasJudiciais">Despesas com a ação, inclusive advogados, pagas pelo contribuinte sem indenização.</param>
/// <param name="TotalParcelasAnosAnteriores">
/// Soma das parcelas de anos anteriores de todo o RRA, quando ele é pago em meses distintos; nula quando este pagamento
/// é o único. Define a quantidade de meses proporcional desta parcela (art. 45, I).
/// </param>
/// <param name="OutrosRendimentosMes">Rendimentos normais do mês pagos pela mesma fonte, somados à parte do ano do pagamento.</param>
public sealed record PagamentoRra(
    DateOnly DataPagamento,
    OrigemPagamentoRra Origem,
    IReadOnlyList<ParcelaRra> Parcelas,
    decimal CorrecaoMonetaria,
    decimal JurosDeMora,
    decimal DespesasJudiciais,
    decimal InssAnosAnteriores,
    decimal PensaoAnosAnteriores,
    decimal InssAnoPagamento,
    decimal PensaoAnoPagamento,
    decimal? TotalParcelasAnosAnteriores,
    bool AplicarReducao,
    decimal OutrosRendimentosMes,
    decimal InssOutrosRendimentos,
    int Dependentes);
