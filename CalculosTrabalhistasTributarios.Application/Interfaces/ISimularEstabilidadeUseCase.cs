using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface ISimularEstabilidadeUseCase
{
    Result<SimulacaoEstabilidadeDto> Executar(SimularEstabilidadeRequest request);
}
