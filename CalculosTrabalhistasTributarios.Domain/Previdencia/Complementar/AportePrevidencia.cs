namespace CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;

/// <summary>Um aporte ao plano: a data, o valor aportado e o saldo atual dele, com os rendimentos.</summary>
public sealed record AportePrevidencia(DateOnly Data, decimal Aportado, decimal Saldo);
