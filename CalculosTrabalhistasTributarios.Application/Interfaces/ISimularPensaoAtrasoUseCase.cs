using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface ISimularPensaoAtrasoUseCase
{
    Task<Result<IReadOnlyList<ParcelaPensaoInformada>>> GerarParcelasAsync(GerarParcelasAtrasoRequest request, CancellationToken cancellationToken);
    Task<Result<SimulacaoPensaoAtrasoDto>> CalcularAsync(SimularPensaoAtrasoRequest request, CancellationToken cancellationToken);
}
