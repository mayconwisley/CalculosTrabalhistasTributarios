using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Gorjetas;

/// <summary>As gorjetas do empregado por mês, a retenção da empresa e as premissas dos reflexos.</summary>
/// <param name="RegimeDiferenciado">Empresa no Simples Nacional, regime federal diferenciado: retenção de até 20%; nos demais, até 33%.</param>
/// <param name="PercentualRetencao">Retenção prevista em convenção ou acordo coletivo para custear os encargos; zero sem previsão.</param>
/// <param name="FeriasGozadas">Falso na dispensa: o reflexo nas férias é indenizado e fica fora do FGTS.</param>
public sealed record EntradaGorjetas(
    IReadOnlyList<MesGorjeta> Meses,
    bool RegimeDiferenciado,
    decimal PercentualRetencao,
    IncidenciasReflexo Incidencias,
    bool FeriasGozadas = true);
