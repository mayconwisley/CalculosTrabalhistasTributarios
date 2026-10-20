using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface IComparacaoHistoricoService
{
    /// <summary>Recalcula os dois cálculos, quando a calculadora permite, e mostra a comparação.</summary>
    Task MostrarAsync(CalculoSalvoDto primeiro, CalculoSalvoDto segundo);

    /// <summary>Compara dois cenários da mesma janela, com os resultados já calculados nela, sem passar pelo histórico.</summary>
    void MostrarCenarios(CalculoSalvoDto cenarioA, DemonstrativoDto resultadoA, CalculoSalvoDto cenarioB, DemonstrativoDto resultadoB);
}
