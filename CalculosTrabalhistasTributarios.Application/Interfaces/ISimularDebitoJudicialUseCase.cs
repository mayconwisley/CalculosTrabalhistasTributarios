using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface ISimularDebitoJudicialUseCase
{
    Task<Result<SimulacaoDebitoJudicialDto>> CalcularAsync(SimularDebitoJudicialRequest request, CancellationToken cancellationToken);
}
