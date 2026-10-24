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
using CalculosTrabalhistasTributarios.Infrastructure.Diagnostico;
using CalculosTrabalhistasTributarios.Mobile.Features.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Features.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Features.Trabalhista;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CalculosTrabalhistasTributarios.Infrastructure.Tabelas;
using CalculosTrabalhistasTributarios.Mobile.Features.Abertura;
using CalculosTrabalhistasTributarios.Mobile.Features.Configuracoes;
using CalculosTrabalhistasTributarios.Mobile.Features.Tabelas;
using CalculosTrabalhistasTributarios.Mobile.Features.Historico;

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
        var registroDeErros = new ArquivoRegistroDeErros(Path.Combine(FileSystem.Current.AppDataDirectory, "logs"), AppInfo.Current.VersionString);
        TratarErrosInesperados(registroDeErros);
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddCalculadoras().AddSaqueAniversario().AddAtualizacaoTodasTabelas().AddPresentation();
        var caminhoBanco = Path.Combine(FileSystem.Current.AppDataDirectory, "BancoDados", "calculoIrrf.db");
        builder.Services.AddInfrastructureLocal(caminhoBanco);
        builder.Services.AddSingleton<SqliteHistoricoCalculos>();
        builder.Services.AddSingleton<IHistoricoCalculos>(p => p.GetRequiredService<SqliteHistoricoCalculos>());
        builder.Services.AddSingleton<IConsultaHistoricoCalculos>(p => p.GetRequiredService<SqliteHistoricoCalculos>());
        builder.Services.AddAtualizacaoTabelas();
        builder.Services.AddSingleton<IConsultaCotacoesTrabalhoExterior, CalculosTrabalhistasTributarios.Infrastructure.Tributacao.ConsultaCotacoesTrabalhoExterior>();
        builder.Services.AddSingleton(provider => new PreparadorBancoLocal(caminhoBanco,
            _ => FileSystem.Current.OpenAppPackageFileAsync("calculoIrrf.db"),
            new InicializadorBancoTributario(provider.GetRequiredService<BancoTributario>()),
            provider.GetRequiredService<SqliteTributacaoConsulta>()));
        builder.Services.Replace(ServiceDescriptor.Singleton<ITributacaoConsulta, TributacaoLocalPreparada>());
        builder.Services.AddSingleton<IRegistroDeErros>(registroDeErros);
        builder.Logging.AddProvider(new RegistroDeErrosLoggerProvider(registroDeErros));
        builder.Services.AddSingleton<IShare>(Share.Default);
        builder.Services.AddSingleton<IFileSystem>(FileSystem.Current);
        builder.Services.AddSingleton<ICompartilhamentoPdf, CompartilhamentoPdf>();
        builder.Services.AddSingleton<IPreferenciasMobile, PreferenciasMobile>();
        builder.Services.AddSingleton<IPlataformaMobile, PlataformaMobile>();
        builder.Services.AddTransient<SaqueAniversarioPage>();
        builder.Services.AddTransient<SimulacaoTributariaPage>();
        builder.Services.AddTransient<InicioPage>();
        builder.Services.AddTransient<MenuPage>();
        builder.Services.AddTransient<AberturaPage>();
        builder.Services.AddTransient<ConfiguracoesPage>();
        builder.Services.AddTransient<HistoricoPage>();
        builder.Services.AddScoped<Func<TipoCalculadoraMobile, Page>>(provider => tipo =>
        {
            Page pagina = tipo switch
            {
            TipoCalculadoraMobile.SimulacaoTributaria => provider.GetRequiredService<SimulacaoTributariaPage>(),
            TipoCalculadoraMobile.SaqueAniversario => provider.GetRequiredService<SaqueAniversarioPage>(),
            _ => new CalculadoraTrabalhistaPage((CalculadoraTrabalhistaViewModel)provider.GetRequiredService(CatalogoCalculadorasMobile.TipoViewModel(tipo)),
                tipo, provider.GetRequiredService<HistoricoMobileService>(), provider.GetRequiredService<ManualMobileService>())
            };
            provider.GetRequiredService<ApoioFormularioMobile>().Configurar(pagina, tipo);
            return pagina;
        });
        // Só o formulário, sem a página: preencher o exemplo e comparar históricos não precisam montar a tela.
        builder.Services.AddScoped<Func<TipoCalculadoraMobile, IFormularioHistoricoMobile>>(provider => tipo => tipo switch
        {
            TipoCalculadoraMobile.SimulacaoTributaria => provider.GetRequiredService<SimulacaoTributariaViewModel>(),
            TipoCalculadoraMobile.SaqueAniversario => provider.GetRequiredService<SaqueAniversarioViewModel>(),
            _ => (IFormularioHistoricoMobile)provider.GetRequiredService(CatalogoCalculadorasMobile.TipoViewModel(tipo))
        });
        builder.Services.AddScoped<NavegadorCalculadoras>();
        builder.Services.AddScoped<INavegacaoCalculadoras>(provider => provider.GetRequiredService<NavegadorCalculadoras>());
        builder.Services.AddScoped<INavegacaoTransferenciasMobile>(provider => provider.GetRequiredService<NavegadorCalculadoras>());
        builder.Services.AddScoped<NavegadorMenu>();
        builder.Services.AddScoped<HistoricoMobileService>();
        builder.Services.AddScoped<IInteracaoHistoricoMobile>(p => p.GetRequiredService<HistoricoMobileService>());
        builder.Services.AddScoped<ApoioFormularioMobile>();
        builder.Services.AddSingleton<ManualMobileService>();
        builder.Services.AddScoped<INavegacaoMenu>(provider => provider.GetRequiredService<NavegadorMenu>());
        builder.Services.AddSingleton<Func<Window>>(provider => () =>
        {
            var scope = provider.CreateScope();
            try
            {
                var abertura = scope.ServiceProvider.GetRequiredService<AberturaPage>();
                var window = new Window(abertura);
                abertura.Preparada += (_, _) =>
                {
                    var inicio = scope.ServiceProvider.GetRequiredService<InicioPage>();
                    var menu = scope.ServiceProvider.GetRequiredService<MenuPage>();
                    var navigation = new NavigationPage(inicio);
                    var flyout = new FlyoutPage { Title = "Menu", Flyout = menu, Detail = navigation };
                    scope.ServiceProvider.GetRequiredService<NavegadorCalculadoras>().Configurar(navigation.Navigation, flyout);
                    scope.ServiceProvider.GetRequiredService<NavegadorMenu>().Configurar(navigation.Navigation, flyout);
                    window.Page = flyout;
                };
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

    /// <summary>
    /// Como no desktop, o erro que escapa das telas fica registrado em logs/erros-AAAA-MM.log, no armazenamento privado do
    /// aparelho; o usuário envia o arquivo pelas Configurações. No Android, a exceção não tratada ainda fecha o aplicativo.
    /// </summary>
    private static void TratarErrosInesperados(IRegistroDeErros registro)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, evento) =>
        {
            if (evento.ExceptionObject is Exception exception)
                registro.Registrar(exception, evento.IsTerminating ? "Erro inesperado que fechou o aplicativo" : "Erro inesperado");
        };
        TaskScheduler.UnobservedTaskException += (_, evento) =>
        {
            registro.Registrar(evento.Exception, "Erro inesperado numa tarefa em segundo plano");
            evento.SetObserved();
        };
#if ANDROID
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, evento) =>
            registro.Registrar(evento.Exception, "Erro inesperado na interface");
#endif
    }
}
