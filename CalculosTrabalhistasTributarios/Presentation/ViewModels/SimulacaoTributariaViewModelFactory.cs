using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed class SimulacaoTributariaViewModelFactory(ISimularImpostoUseCase simularImposto, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha, IArquivoDialogService arquivoDialog, ContextoCompartilhado contexto, IHistoricoDaJanelaFactory historico) : ISimulacaoTributariaViewModelFactory
{
    public SimulacaoTributariaViewModel Criar() => new(simularImposto, notificador, relatorioPdf, planilha, arquivoDialog, contexto, historico);
}
