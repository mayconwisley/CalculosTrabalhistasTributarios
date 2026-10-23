using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence;

public sealed class TributacaoLocalPreparada(PreparadorBancoLocal preparador, SqliteTributacaoConsulta consulta) : ITributacaoConsulta
{
    public Task<Result<PerfilTributario>> ObterPerfilAsync(DateOnly competencia, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            await preparador.PrepararAsync(cancellationToken);
            return await consulta.ObterPerfilAsync(competencia, cancellationToken);
        }, cancellationToken);

    public Task<decimal?> ObterSalarioMinimoAsync(DateOnly competencia, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            await preparador.PrepararAsync(cancellationToken);
            return await consulta.ObterSalarioMinimoAsync(competencia, cancellationToken);
        }, cancellationToken);
}
