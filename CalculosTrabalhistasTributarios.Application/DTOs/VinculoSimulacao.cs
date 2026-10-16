namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>
/// Tipo de vínculo da simulação tributária. Os três primeiros definem o FGTS; na aposentadoria ou pensão, paga pelo INSS
/// ou pela previdência complementar, não há INSS nem FGTS.
/// </summary>
public enum VinculoSimulacao { Empregado, Aprendiz, Domestico, Aposentadoria }
