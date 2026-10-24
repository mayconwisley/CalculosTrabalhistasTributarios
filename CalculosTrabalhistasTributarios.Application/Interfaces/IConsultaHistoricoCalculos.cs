using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface IConsultaHistoricoCalculos
{
    Task<PaginaHistoricoDto> ConsultarAsync(ConsultaHistoricoDto consulta, CancellationToken cancellationToken);
}
