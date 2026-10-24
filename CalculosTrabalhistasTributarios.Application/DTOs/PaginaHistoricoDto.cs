namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record PaginaHistoricoDto(IReadOnlyList<ResumoCalculoSalvoDto> Itens, int Total, int TotalGeral, IReadOnlyList<string> Calculadoras);
