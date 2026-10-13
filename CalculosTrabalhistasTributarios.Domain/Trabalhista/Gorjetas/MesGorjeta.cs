namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Gorjetas;

/// <summary>A parte do empregado nas gorjetas de uma competência, antes da retenção pela empresa.</summary>
public sealed record MesGorjeta(DateOnly Competencia, decimal Valor);
