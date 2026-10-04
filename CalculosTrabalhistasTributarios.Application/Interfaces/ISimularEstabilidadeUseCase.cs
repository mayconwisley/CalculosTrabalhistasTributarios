using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface ISimularEstabilidadeUseCase
{
    Result<SimulacaoEstabilidadeDto> Executar(SimularEstabilidadeRequest request);
}
