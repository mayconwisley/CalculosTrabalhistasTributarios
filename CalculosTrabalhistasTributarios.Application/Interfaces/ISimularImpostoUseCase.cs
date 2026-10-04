using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface ISimularImpostoUseCase
{
    Task<Result<SimulacaoImpostoDto>> ExecutarAsync(SimularImpostoRequest request, CancellationToken cancellationToken);
}
