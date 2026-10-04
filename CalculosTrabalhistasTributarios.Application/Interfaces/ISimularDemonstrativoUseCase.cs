using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>Calculadora que devolve o resultado como demonstrativo (13º, férias, rescisão e as demais).</summary>
public interface ISimularDemonstrativoUseCase<in TRequest>
{
    Task<Result<DemonstrativoDto>> ExecutarAsync(TRequest request, CancellationToken cancellationToken);
}
