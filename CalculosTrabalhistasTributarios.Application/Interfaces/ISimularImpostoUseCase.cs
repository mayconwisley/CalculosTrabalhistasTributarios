using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface ISimularImpostoUseCase
{
    Task<Result<SimulacaoImpostoDto>> ExecutarAsync(SimularImpostoRequest request, CancellationToken cancellationToken);
}
