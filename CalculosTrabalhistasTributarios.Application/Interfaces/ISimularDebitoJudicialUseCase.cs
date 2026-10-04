using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface ISimularDebitoJudicialUseCase
{
    Task<Result<SimulacaoDebitoJudicialDto>> CalcularAsync(SimularDebitoJudicialRequest request, CancellationToken cancellationToken);
}
