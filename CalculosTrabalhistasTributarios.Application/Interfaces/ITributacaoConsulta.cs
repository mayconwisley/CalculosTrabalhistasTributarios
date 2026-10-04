using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>Porta de leitura das tabelas tributárias. A aplicação não conhece EF Core.</summary>
public interface ITributacaoConsulta
{
    Task<Result<PerfilTributario>> ObterPerfilAsync(DateOnly competencia, CancellationToken cancellationToken);

    /// <summary>Salário mínimo vigente na competência, sem depender das tabelas de INSS e IRRF; nulo antes do primeiro cadastrado.</summary>
    Task<decimal?> ObterSalarioMinimoAsync(DateOnly competencia, CancellationToken cancellationToken);
}
