namespace CalculosTrabalhistasTributarios.Domain.Judicial;

/// <summary>
/// Acréscimos do cumprimento de sentença pelo rito da penhora quando o débito não é pago em 15 dias da intimação: multa de
/// 10% e honorários de 10% (CPC, art. 523, § 1º). Não se aplicam ao rito da prisão.
/// </summary>
public enum AcrescimosPenhora { Nenhum, MultaEHonorarios, Multa }
