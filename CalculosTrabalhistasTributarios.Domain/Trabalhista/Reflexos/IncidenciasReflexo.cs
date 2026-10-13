namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <summary>Em que a verba reflete. Quais reflexos cabem é uma premissa de quem chama: o motor não presume o direito.</summary>
[Flags]
public enum IncidenciasReflexo
{
    Nenhuma = 0,
    Dsr = 1,
    DecimoTerceiro = 2,
    Ferias = 4,
    AvisoPrevio = 8,
    Fgts = 16,
    MultaFgts = 32
}
