using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface ITabelaTributariaService
{
    Task<IReadOnlyList<RegistroTabelaDto>> ListarAsync(TipoTabelaTributaria tipo, CancellationToken cancellationToken);
    Task<Result> SalvarAsync(TipoTabelaTributaria tipo, SalvarRegistroTabelaRequest request, CancellationToken cancellationToken);
    Task<Result> ExcluirAsync(TipoTabelaTributaria tipo, int id, CancellationToken cancellationToken);
}
