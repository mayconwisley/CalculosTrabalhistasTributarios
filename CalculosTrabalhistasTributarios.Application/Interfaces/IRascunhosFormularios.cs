using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface IRascunhosFormularios
{
    Task<RascunhoFormularioDto?> ObterAsync(string chave, CancellationToken cancellationToken);
    Task SalvarAsync(string chave, RascunhoFormularioDto rascunho, CancellationToken cancellationToken);
    Task RemoverAsync(string chave, CancellationToken cancellationToken);
}
