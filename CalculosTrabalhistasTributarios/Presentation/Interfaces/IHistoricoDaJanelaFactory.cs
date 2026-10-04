using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface IHistoricoDaJanelaFactory
{
    HistoricoDaJanela Criar(ICalculoSalvavel calculo);
}
