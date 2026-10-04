using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed class PensaoAtrasoViewModelFactory(ISimularPensaoAtrasoUseCase simulador, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha, IArquivoDialogService arquivoDialog, IHistoricoDaJanelaFactory historico) : IPensaoAtrasoViewModelFactory
{
    public PensaoAtrasoViewModel Criar() => new(simulador, notificador, relatorioPdf, planilha, arquivoDialog, historico);
}
