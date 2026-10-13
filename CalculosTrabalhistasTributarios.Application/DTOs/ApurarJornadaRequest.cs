using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Competencia">Mês da folha em que as horas apuradas são pagas.</param>
/// <param name="Dias">Dias do período do ponto, que pode começar num mês e terminar no seguinte, como de 16/09 a 15/10.</param>
/// <param name="Escala12x36">Jornada de 12 horas por 36 de descanso (CLT, art. 59-A): o feriado num dia da escala é compensado.</param>
public sealed record ApurarJornadaRequest(DateOnly Competencia, TrabalhoNoturno Noturno, IReadOnlyList<MarcacaoDia> Dias, bool Escala12x36 = false)
{
    /// <summary>Até dois meses de marcações: cobre fechamentos de ponto mais longos sem gerar uma grade sem fim.</summary>
    public const int MaximoDias = 62;
}
