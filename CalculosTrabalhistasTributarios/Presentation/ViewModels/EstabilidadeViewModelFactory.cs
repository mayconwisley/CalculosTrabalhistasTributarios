using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed class EstabilidadeViewModelFactory(ISimularEstabilidadeUseCase simulador, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha, IArquivoDialogService arquivoDialog, IHistoricoDaJanelaFactory historico) : IEstabilidadeViewModelFactory
{
    public EstabilidadeViewModel Criar() => new(simulador, notificador, relatorioPdf, planilha, arquivoDialog, historico);
}
