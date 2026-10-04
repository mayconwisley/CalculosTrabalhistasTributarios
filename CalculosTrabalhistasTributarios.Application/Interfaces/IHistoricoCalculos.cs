using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>Cálculos salvos pelo usuário, para reabrir, refazer com outros valores ou duplicar.</summary>
public interface IHistoricoCalculos
{
    /// <summary>Do alterado mais recentemente para o mais antigo.</summary>
    Task<IReadOnlyList<CalculoSalvoDto>> ListarAsync(CancellationToken cancellationToken);

    Task<CalculoSalvoDto?> ObterAsync(long id, CancellationToken cancellationToken);

    /// <summary>Inclui um cálculo novo, sem <paramref name="id"/>, ou substitui o salvo com ele; devolve o id.</summary>
    Task<long> SalvarAsync(long? id, string tipo, string calculadora, string nome, string dados, CancellationToken cancellationToken);

    /// <summary>Cópia com o nome seguido de "(cópia)"; devolve o id da cópia.</summary>
    Task<Result<long>> DuplicarAsync(long id, CancellationToken cancellationToken);

    Task RenomearAsync(long id, string nome, CancellationToken cancellationToken);

    Task ExcluirAsync(long id, CancellationToken cancellationToken);
}
