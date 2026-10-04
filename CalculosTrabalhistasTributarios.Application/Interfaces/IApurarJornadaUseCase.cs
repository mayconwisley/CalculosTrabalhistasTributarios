using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface IApurarJornadaUseCase
{
    Result<SimulacaoJornadaDto> Apurar(ApurarJornadaRequest request);
}
