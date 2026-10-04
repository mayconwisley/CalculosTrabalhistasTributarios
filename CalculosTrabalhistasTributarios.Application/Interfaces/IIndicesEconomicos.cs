using CalculosTrabalhistasTributarios.Domain.Judicial;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>Séries mensais dos índices econômicos: a variação (INPC e IPCA) ou a taxa (taxa legal) de cada mês, em %.</summary>
public interface IIndicesEconomicos
{
    /// <summary>Valor de cada mês cadastrado, com a competência no primeiro dia do mês.</summary>
    Task<IReadOnlyDictionary<DateOnly, decimal>> ObterAsync(IndiceEconomico indice, CancellationToken cancellationToken);
}
