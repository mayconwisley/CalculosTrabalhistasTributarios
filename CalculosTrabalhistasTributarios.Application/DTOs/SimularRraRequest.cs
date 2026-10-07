using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Pagamento de rendimentos recebidos acumuladamente, com as parcelas por competência e as deduções.</summary>
public sealed record SimularRraRequest(PagamentoRra Pagamento);
