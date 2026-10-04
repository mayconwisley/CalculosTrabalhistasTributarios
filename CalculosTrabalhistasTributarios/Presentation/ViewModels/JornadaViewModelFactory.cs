using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed class JornadaViewModelFactory(IApurarJornadaUseCase apuracao, IUserNotifier notificador, IPlanilhaService planilha, IArquivoDialogService arquivoDialog,
    IServiceProvider servicos, ContextoCompartilhado contexto, IHistoricoDaJanelaFactory historico) : IJornadaViewModelFactory
{
    // O navegador é obtido na hora, porque ele mesmo depende desta fábrica.
    public JornadaViewModel Criar() => new(apuracao, notificador, planilha, arquivoDialog,
        (IWindowNavigator)servicos.GetService(typeof(IWindowNavigator))!, contexto, historico);
}
