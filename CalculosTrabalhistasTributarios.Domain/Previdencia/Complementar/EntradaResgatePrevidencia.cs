namespace CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;

/// <summary>O resgate a simular, com os aportes do plano.</summary>
/// <param name="ValorResgate">Valor bruto do resgate parcial; nulo para resgatar o saldo total.</param>
public sealed record EntradaResgatePrevidencia(
    TipoPlanoPrevidencia Tipo,
    RegimeTributacaoPrevidencia Regime,
    IReadOnlyList<AportePrevidencia> Aportes,
    DateOnly DataResgate,
    decimal? ValorResgate = null);
