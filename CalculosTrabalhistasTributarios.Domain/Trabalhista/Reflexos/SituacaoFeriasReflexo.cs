namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <summary>
/// Situação das férias de um período aquisitivo no reflexo: gozadas (com FGTS), indenizadas na rescisão, vencidas ou
/// proporcionais (sem FGTS, OJ 195 da SBDI-1 do TST), ou ainda não devidas, no período em curso de um contrato ativo.
/// </summary>
public enum SituacaoFeriasReflexo { Gozadas, Indenizadas, NaoDevidas }
