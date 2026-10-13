using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Transferencia;

/// <summary>Os meses da transferência provisória e as premissas dos reflexos.</summary>
/// <param name="Percentual">Percentual do adicional, de no mínimo 25% (CLT, art. 469, § 3º); a norma coletiva pode prever mais.</param>
/// <param name="Despesas">Despesas da mudança pagas pelo empregador (CLT, art. 470): reembolso, sem natureza salarial nem reflexos.</param>
/// <param name="FeriasGozadas">Falso na dispensa: o reflexo nas férias é indenizado e fica fora do FGTS.</param>
public sealed record EntradaTransferencia(
    IReadOnlyList<MesTransferencia> Meses,
    decimal Percentual,
    IncidenciasReflexo Incidencias,
    decimal Despesas = 0m,
    int DiasAvisoPrevio = 0,
    bool FeriasGozadas = true);
