using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Mobile.Presentation;
using CalculosTrabalhistasTributarios.Mobile.Features.SaqueAniversario;
using Microsoft.Extensions.Logging;
using System.Globalization;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Services;
using CalculosTrabalhistasTributarios.Infrastructure.Local;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Features.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Features.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Features.Trabalhista;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CalculosTrabalhistasTributarios.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddCalculadoras().AddSaqueAniversario().AddPresentation();
        var caminhoBanco = Path.Combine(FileSystem.Current.AppDataDirectory, "BancoDados", "calculoIrrf.db");
        builder.Services.AddInfrastructureLocal(caminhoBanco);
        builder.Services.AddSingleton(provider => new PreparadorBancoLocal(caminhoBanco,
            _ => FileSystem.Current.OpenAppPackageFileAsync("calculoIrrf.db"),
            new InicializadorBancoTributario(provider.GetRequiredService<BancoTributario>()),
            provider.GetRequiredService<SqliteTributacaoConsulta>()));
        builder.Services.Replace(ServiceDescriptor.Singleton<ITributacaoConsulta, TributacaoLocalPreparada>());
        builder.Services.AddSingleton<IShare>(Share.Default);
        builder.Services.AddSingleton<IFileSystem>(FileSystem.Current);
        builder.Services.AddSingleton<ICompartilhamentoPdf, CompartilhamentoPdf>();
        builder.Services.AddTransient<SaqueAniversarioPage>();
        builder.Services.AddTransient<SimulacaoTributariaPage>();
        builder.Services.AddTransient<InicioPage>();
        builder.Services.AddScoped<Func<TipoCalculadoraMobile, Page>>(provider => tipo => tipo switch
        {
            TipoCalculadoraMobile.SimulacaoTributaria => provider.GetRequiredService<SimulacaoTributariaPage>(),
            TipoCalculadoraMobile.SaqueAniversario => provider.GetRequiredService<SaqueAniversarioPage>(),
            _ => new CalculadoraTrabalhistaPage((CalculadoraTrabalhistaViewModel)provider.GetRequiredService(CatalogoCalculadorasMobile.TipoViewModel(tipo)))
        });
        builder.Services.AddScoped<NavegadorCalculadoras>();
        builder.Services.AddScoped<INavegacaoCalculadoras>(provider => provider.GetRequiredService<NavegadorCalculadoras>());
        builder.Services.AddSingleton<Func<Window>>(provider => () =>
        {
            var scope = provider.CreateScope();
            try
            {
                var page = scope.ServiceProvider.GetRequiredService<InicioPage>();
                var navigation = new NavigationPage(page);
                scope.ServiceProvider.GetRequiredService<NavegadorCalculadoras>().Configurar(navigation.Navigation);
                var window = new Window(navigation);
                window.Destroying += (_, _) => scope.Dispose();
                return window;
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        });
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
