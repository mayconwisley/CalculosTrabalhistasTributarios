using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Infrastructure;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace CalculosTrabalhistasTributarios;

[SupportedOSPlatform("windows")]
public partial class App : System.Windows.Application
{
    private ServiceProvider _serviceProvider = null!;
    private IServiceScope _applicationScope = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // O WPF formata bindings (StringFormat) em en-US por padrão; o app inteiro exibe valores no padrão brasileiro.
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("pt-BR")));
        ThemeManager.Initialize();
        if (!ThemeManager.UseHardwareAcceleration)
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        _serviceProvider = ConfigureServices();
        // ApplicationCommands.Help já responde ao F1; registrado em Window, vale para todas as janelas e para o botão "Manual".
        var manual = _serviceProvider.GetRequiredService<IManualUsuarioService>();
        CommandManager.RegisterClassCommandBinding(typeof(Window), new CommandBinding(ApplicationCommands.Help, (_, _) => manual.Abrir()));
        _applicationScope = _serviceProvider.CreateScope();
        _applicationScope.ServiceProvider.GetRequiredService<IInicializadorBancoTributario>()
            .InicializarAsync(CancellationToken.None).GetAwaiter().GetResult();
        _applicationScope.ServiceProvider.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeManager.Dispose();
        _applicationScope?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    // Cada camada registra os seus serviços; a composição fica aqui, no ponto de entrada.
    private static ServiceProvider ConfigureServices() => new ServiceCollection()
        .AddApplication()
        .AddInfrastructure()
        .AddPresentation()
        .BuildServiceProvider();
}
