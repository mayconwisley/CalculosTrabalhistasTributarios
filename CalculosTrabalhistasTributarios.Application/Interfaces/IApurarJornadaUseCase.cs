using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface IApurarJornadaUseCase
{
    Result<SimulacaoJornadaDto> Apurar(ApurarJornadaRequest request);
}
