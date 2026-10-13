namespace CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;

/// <summary>
/// PGBL: as contribuições são dedutíveis na declaração, e o IR do resgate incide sobre o valor total resgatado.
/// VGBL: não há dedução, e o IR incide só sobre os rendimentos.
/// </summary>
public enum TipoPlanoPrevidencia { Pgbl, Vgbl }
