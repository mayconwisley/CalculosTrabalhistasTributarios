using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure;
using CalculosTrabalhistasTributarios.Infrastructure.Diagnostico;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.Behaviors;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
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

    // async void: OnStartup é o ponto de entrada do WPF, sem quem aguarde a tarefa; toda falha é tratada aqui dentro.
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TratarErrosInesperados();
        // O WPF formata bindings (StringFormat) em en-US por padrão; o app inteiro exibe valores no padrão brasileiro.
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("pt-BR")));
        ThemeManager.Initialize();
        if (!ThemeManager.UseHardwareAcceleration)
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        // A tela de abertura fecha antes de a principal ser a única janela; o fim do aplicativo é decidido abaixo.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        AberturaWindow? abertura = null;
        try
        {
            _serviceProvider = ConfigureServices();
            // ApplicationCommands.Help já responde ao F1; registrado em Window, vale para todas as janelas, para o botão "Manual"
            // e para o "?" das calculadoras. Cada janela abre o manual na sua seção, pela chave com que guarda a posição.
            var manual = _serviceProvider.GetRequiredService<IManualUsuarioService>();
            CommandManager.RegisterClassCommandBinding(typeof(Window), new CommandBinding(ApplicationCommands.Help,
                (janela, _) => manual.Abrir(JanelaLembrada.GetChave((DependencyObject)janela))));
            CommandManager.RegisterClassCommandBinding(typeof(Window), new CommandBinding(AtalhosDeTeclado.Mostrar,
                (janela, _) => CaixaMensagemWindow.Mostrar(AtalhosDeTeclado.Texto, "Atalhos de teclado", TipoCaixaMensagem.Informacao, (Window)janela)));
            _applicationScope = _serviceProvider.CreateScope();
            var servicos = _applicationScope.ServiceProvider;

            // A abertura prepara o banco fora da thread da tela, para a barra continuar respondendo, e atualiza as tabelas.
            abertura = servicos.GetRequiredService<AberturaWindow>();
            var progresso = (AberturaViewModel)abertura.DataContext;
            abertura.Show();
            var inicializador = servicos.GetRequiredService<IInicializadorBancoTributario>();
            await progresso.ExecutarAsync(() => Task.Run(() => inicializador.InicializarAsync(CancellationToken.None)), DateOnly.FromDateTime(DateTime.Today));

            var janela = servicos.GetRequiredService<MainWindow>();
            servicos.GetRequiredService<MainWindowViewModel>().InformarAtualizacaoAoAbrir(progresso.Resultados);
            MainWindow = janela;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            janela.Show();
            abertura.Close();
            MostrarAvisoDeSimulacao(janela);
        }
        catch (Exception exception)
        {
            // Sem o banco ou os serviços não há o que mostrar: o usuário precisa saber o motivo, e não ver o app sumir.
            _registroDeErros.Registrar(exception, "Falha ao abrir o aplicativo");
            abertura?.Close();
            CaixaMensagemWindow.Mostrar(
                $"Não foi possível abrir o aplicativo.\n\n{exception.Message}\n\nOs detalhes foram registrados em:\n{_registroDeErros.Arquivo}\n\n" +
                "Se o problema continuar, reinstale o aplicativo: as tabelas cadastradas ficam preservadas na pasta BancoDados.",
                Titulo, TipoCaixaMensagem.Erro);
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
            CaixaMensagemWindow.Mostrar(
                $"Ocorreu um erro inesperado, e a última ação pode não ter sido concluída.\n\n{evento.Exception.Message}\n\nOs detalhes foram registrados em:\n{_registroDeErros.Arquivo}",
                Titulo, TipoCaixaMensagem.Erro);
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
        CaixaMensagemWindow.Mostrar(
            "Os resultados deste aplicativo são simulações feitas com a legislação e as tabelas cadastradas.\n\n" +
            "Antes de usá-los num pagamento, numa rescisão ou num processo, confira a competência, as tabelas e a convenção coletiva " +
            "da categoria. Procure um profissional especializado no cálculo para apurar e validar os valores aplicáveis ao seu caso.\n\n" +
            "O manual do usuário abre pelo botão Manual do usuário ou pela tecla F1. Os Termos de Uso, a Licença de uso, " +
            "a Responsabilidade de uso e a Política de Privacidade ficam em Configurações e no rodapé da tela inicial.",
            "Antes de começar", TipoCaixaMensagem.Informacao, janela);
        ConfiguracoesUsuario.Alterar(configuracoes => configuracoes.AvisoSimulacaoLido = true);
    }
}
