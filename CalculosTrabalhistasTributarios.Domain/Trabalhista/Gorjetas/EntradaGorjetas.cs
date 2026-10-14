using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Gorjetas;

/// <summary>As gorjetas do empregado por mês, a retenção da empresa e as premissas dos reflexos.</summary>
/// <param name="RegimeDiferenciado">Empresa no Simples Nacional, regime federal diferenciado: retenção de até 20%; nos demais, até 33%.</param>
/// <param name="PercentualRetencao">Retenção prevista em convenção ou acordo coletivo para custear os encargos; zero sem previsão.</param>
/// <param name="FeriasGozadas">Verdadeiro no contrato ativo; falso na dispensa, quando as férias não gozadas são indenizadas, sem FGTS.</param>
/// <param name="Admissao">Data de admissão, que separa os períodos aquisitivos das férias; nula para somar todos os meses.</param>
public sealed record EntradaGorjetas(
    IReadOnlyList<MesGorjeta> Meses,
    bool RegimeDiferenciado,
    decimal PercentualRetencao,
    IncidenciasReflexo Incidencias,
    bool FeriasGozadas = true,
    DateOnly? Admissao = null);
