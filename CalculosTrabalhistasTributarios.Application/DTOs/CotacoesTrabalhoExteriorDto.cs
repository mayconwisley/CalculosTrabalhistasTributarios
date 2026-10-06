namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record CotacoesTrabalhoExteriorDto(decimal DolarCompraFiscal, decimal? DolaresPorUnidade, DateOnly Recebimento);
