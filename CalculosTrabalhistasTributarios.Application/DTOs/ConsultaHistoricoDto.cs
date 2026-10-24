namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record ConsultaHistoricoDto(string? Busca = null, string? Calculadora = null,
    DateTime? Desde = null, DateTime? AteExclusivo = null, OrdemHistorico Ordem = OrdemHistorico.MaisRecentes,
    int Deslocamento = 0, int Limite = 20);
