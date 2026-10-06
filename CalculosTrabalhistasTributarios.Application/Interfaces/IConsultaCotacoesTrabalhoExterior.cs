using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface IConsultaCotacoesTrabalhoExterior
{
    Task<Result<CotacoesTrabalhoExteriorDto>> ConsultarAsync(DateOnly recebimento, string moeda, CancellationToken cancellationToken);
}
