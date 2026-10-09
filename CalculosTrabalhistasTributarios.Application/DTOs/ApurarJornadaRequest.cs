using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Competencia">Mês da folha em que as horas apuradas são pagas.</param>
/// <param name="Dias">Dias do período do ponto, que pode começar num mês e terminar no seguinte, como de 16/09 a 15/10.</param>
public sealed record ApurarJornadaRequest(DateOnly Competencia, TrabalhoNoturno Noturno, IReadOnlyList<MarcacaoDia> Dias)
{
    /// <summary>Até dois meses de marcações: cobre fechamentos de ponto mais longos sem gerar uma grade sem fim.</summary>
    public const int MaximoDias = 62;
}
