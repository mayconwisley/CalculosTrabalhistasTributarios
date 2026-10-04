using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

public sealed class HistoricoDaJanelaFactory(IHistoricoCalculos historico, INomeDialogService dialogo, IUserNotifier notificador) : IHistoricoDaJanelaFactory
{
    public HistoricoDaJanela Criar(ICalculoSalvavel calculo) => new(historico, dialogo, notificador, calculo);
}
