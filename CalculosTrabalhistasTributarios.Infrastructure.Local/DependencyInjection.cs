using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CalculosTrabalhistasTributarios.Infrastructure.Local;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureLocal(this IServiceCollection services, string caminhoBanco) => services
        .AddSingleton<IRascunhosFormularios>(_ => new ArquivoRascunhosFormularios(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(caminhoBanco)) ?? throw new ArgumentException("Diretório do banco inválido.", nameof(caminhoBanco)), "rascunhos")))
        .AddSingleton(new BancoTributario(new SqliteConnectionStringBuilder { DataSource = caminhoBanco }.ToString()))
        .AddSingleton<SqliteTributacaoConsulta>()
        .AddSingleton<ITributacaoConsulta>(provider => provider.GetRequiredService<SqliteTributacaoConsulta>())
        .AddSingleton<ICacheTabelasTributarias>(provider => provider.GetRequiredService<SqliteTributacaoConsulta>())
        .AddSingleton<IIndicesEconomicos, SqliteIndicesEconomicos>()
        .AddScoped<IInicializadorBancoTributario, InicializadorBancoTributario>();
}
