namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Vencimento">Data de vencimento da guia, já prorrogada quando cai em fim de semana ou feriado.</param>
public sealed record SimularTributoAtrasoRequest(GuiaDeRecolhimento Guia, decimal Principal, DateOnly Vencimento, DateOnly Pagamento);
