using System.IO;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Os serviços do aplicativo sobre uma cópia nova do banco do projeto, inicializada uma vez por execução dos testes,
/// como na primeira abertura do aplicativo.
/// </summary>
internal static class Ambiente
{
    private static readonly Lazy<Task<IServiceProvider>> Servicos = new(CriarAsync);

    public static async Task<ITributacaoConsulta> ConsultaAsync() => (await Servicos.Value).GetRequiredService<ITributacaoConsulta>();

    public static async Task<IIndicesEconomicos> IndicesAsync() => (await Servicos.Value).GetRequiredService<IIndicesEconomicos>();

    public static async Task<IHistoricoCalculos> HistoricoAsync() => (await Servicos.Value).GetRequiredService<IHistoricoCalculos>();

    private static async Task<IServiceProvider> CriarAsync()
    {
        var pasta = Path.Combine(AppContext.BaseDirectory, "BancoDados");
        File.Copy(Path.Combine(pasta, "base.db"), Path.Combine(pasta, "calculoIrrf.db"), overwrite: true);
        var escopo = new ServiceCollection().AddInfrastructure().BuildServiceProvider().CreateScope();
        await escopo.ServiceProvider.GetRequiredService<IInicializadorBancoTributario>().InicializarAsync(CancellationToken.None);
        return escopo.ServiceProvider;
    }
}
