using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface IComparacaoHistoricoService
{
    /// <summary>Recalcula os dois cálculos, quando a calculadora permite, e mostra a comparação.</summary>
    Task MostrarAsync(CalculoSalvoDto primeiro, CalculoSalvoDto segundo);
}
