using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface ISimularPensaoUseCase
{
    Task<Result<SimulacaoPensaoDto>> ExecutarAsync(SimularPensaoRequest request, CancellationToken cancellationToken);
}
