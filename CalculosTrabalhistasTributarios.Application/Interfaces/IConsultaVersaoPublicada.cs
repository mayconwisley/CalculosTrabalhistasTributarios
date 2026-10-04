using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>A versão mais recente publicada do aplicativo.</summary>
public interface IConsultaVersaoPublicada
{
    Task<Result<VersaoPublicada>> ConsultarAsync(CancellationToken cancellationToken);
}
