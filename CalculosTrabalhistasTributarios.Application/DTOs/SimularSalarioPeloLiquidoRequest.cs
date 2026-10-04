namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record SimularSalarioPeloLiquidoRequest(DateOnly Competencia, decimal LiquidoDesejado, int Dependentes);
