using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface ISimularPensaoAtrasoUseCase
{
    Task<Result<IReadOnlyList<ParcelaPensaoInformada>>> GerarParcelasAsync(GerarParcelasAtrasoRequest request, CancellationToken cancellationToken);
    Task<Result<SimulacaoPensaoAtrasoDto>> CalcularAsync(SimularPensaoAtrasoRequest request, CancellationToken cancellationToken);
}
