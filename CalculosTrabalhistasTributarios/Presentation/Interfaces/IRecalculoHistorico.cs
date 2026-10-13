using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

/// <summary>Recalcula um cálculo salvo da janela padrão, sem abri-lo, para comparar os resultados de dois cenários.</summary>
public interface IRecalculoHistorico
{
    /// <summary>Falso para as janelas próprias, como a pensão e a jornada, que não produzem um demonstrativo padrão.</summary>
    bool Suporta(CalculoSalvoDto salvo);

    Task<Result<DemonstrativoDto>> RecalcularAsync(CalculoSalvoDto salvo, CancellationToken cancellationToken);
}
