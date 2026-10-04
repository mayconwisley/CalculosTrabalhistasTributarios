using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed class DebitoJudicialViewModelFactory(ISimularDebitoJudicialUseCase simulador, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha,
    IArquivoDialogService arquivoDialog, IHistoricoDaJanelaFactory historico) : IDebitoJudicialViewModelFactory
{
    public DebitoJudicialViewModel Criar() => new(simulador, notificador, relatorioPdf, planilha, arquivoDialog, historico);
}
