using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface ISimularPensaoUseCase
{
    Task<Result<SimulacaoPensaoDto>> ExecutarAsync(SimularPensaoRequest request, CancellationToken cancellationToken);
}
