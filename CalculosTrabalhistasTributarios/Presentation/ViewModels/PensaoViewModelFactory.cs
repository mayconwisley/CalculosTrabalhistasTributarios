using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed class PensaoViewModelFactory(ISimularPensaoUseCase simulador, ITributacaoConsulta tributacao, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha, IArquivoDialogService arquivoDialog, ContextoCompartilhado contexto, IHistoricoDaJanelaFactory historico) : IPensaoViewModelFactory
{
    public PensaoViewModel Criar() => new(simulador, tributacao, notificador, relatorioPdf, planilha, arquivoDialog, contexto, historico);
}
