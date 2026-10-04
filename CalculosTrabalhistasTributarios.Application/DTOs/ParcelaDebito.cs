namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record ParcelaDebito(string Descricao, DateOnly Vencimento, decimal Valor);
