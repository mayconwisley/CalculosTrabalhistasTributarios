using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure;
using CalculosTrabalhistasTributarios.Infrastructure.Diagnostico;
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
    private const string Titulo = "Cálculos Trabalhistas e Tributários";

    // Criado antes de tudo, para registrar até uma falha na montagem dos serviços.
    private readonly IRegistroDeErros _registroDeErros = new ArquivoRegistroDeErros();
    private ServiceProvider? _serviceProvider;
    private IServiceScope? _applicationScope;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TratarErrosInesperados();
        // O WPF formata bindings (StringFormat) em en-US por padrão; o app inteiro exibe valores no padrão brasileiro.
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("pt-BR")));
        ThemeManager.Initialize();
        if (!ThemeManager.UseHardwareAcceleration)
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        try
        {
            _serviceProvider = ConfigureServices();
            // ApplicationCommands.Help já responde ao F1; registrado em Window, vale para todas as janelas e para o botão "Manual".
            var manual = _serviceProvider.GetRequiredService<IManualUsuarioService>();
            CommandManager.RegisterClassCommandBinding(typeof(Window), new CommandBinding(ApplicationCommands.Help, (_, _) => manual.Abrir()));
            _applicationScope = _serviceProvider.CreateScope();
            _applicationScope.ServiceProvider.GetRequiredService<IInicializadorBancoTributario>()
                .InicializarAsync(CancellationToken.None).GetAwaiter().GetResult();
            var janela = _applicationScope.ServiceProvider.GetRequiredService<MainWindow>();
            janela.Show();
            MostrarAvisoDeSimulacao(janela);
        }
        catch (Exception exception)
        {
            // Sem o banco ou os serviços não há o que mostrar: o usuário precisa saber o motivo, e não ver o app sumir.
            _registroDeErros.Registrar(exception, "Falha ao abrir o aplicativo");
            MessageBox.Show(
                $"Não foi possível abrir o aplicativo.\n\n{exception.Message}\n\nOs detalhes foram registrados em:\n{_registroDeErros.Arquivo}\n\n" +
                "Se o problema continuar, reinstale o aplicativo: as tabelas cadastradas ficam preservadas na pasta BancoDados.",
                Titulo, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
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

    /// <summary>
    /// Um erro que escapou do tratamento das telas é registrado e mostrado; na interface, o aplicativo continua aberto,
    /// para o usuário não perder os outros cálculos abertos.
    /// </summary>
    private void TratarErrosInesperados()
    {
        DispatcherUnhandledException += (_, evento) =>
        {
            _registroDeErros.Registrar(evento.Exception, "Erro inesperado na interface");
            MessageBox.Show(
                $"Ocorreu um erro inesperado, e a última ação pode não ter sido concluída.\n\n{evento.Exception.Message}\n\nOs detalhes foram registrados em:\n{_registroDeErros.Arquivo}",
                Titulo, MessageBoxButton.OK, MessageBoxImage.Error);
            evento.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, evento) =>
        {
            if (evento.ExceptionObject is Exception exception)
                _registroDeErros.Registrar(exception, evento.IsTerminating ? "Erro inesperado que fechou o aplicativo" : "Erro inesperado");
        };
        TaskScheduler.UnobservedTaskException += (_, evento) =>
        {
            _registroDeErros.Registrar(evento.Exception, "Erro inesperado numa tarefa em segundo plano");
            evento.SetObserved();
        };
    }

    // Uma vez só: os resultados são simulações, e quem usa precisa saber disso antes de pagar ou cobrar um valor.
    private static void MostrarAvisoDeSimulacao(Window janela)
    {
        if (ConfiguracoesUsuario.Atuais.AvisoSimulacaoLido)
            return;
        MessageBox.Show(janela,
            "Os resultados deste aplicativo são simulações feitas com a legislação e as tabelas cadastradas.\n\n" +
            "Antes de usá-los num pagamento, numa rescisão ou num processo, confira a competência, as tabelas e a convenção coletiva " +
            "da categoria. Procure um profissional especializado no cálculo para apurar e validar os valores aplicáveis ao seu caso.\n\n" +
            "O manual do usuário abre pelo botão Manual do usuário ou pela tecla F1.",
            "Antes de começar", MessageBoxButton.OK, MessageBoxImage.Information);
        ConfiguracoesUsuario.Alterar(configuracoes => configuracoes.AvisoSimulacaoLido = true);
    }
}
