using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

public sealed class TabelaManutencaoViewModelFactory(
    ITabelaTributariaService service,
    IAtualizadorTabelas atualizador,
    IUserNotifier notificador) : ITabelaManutencaoViewModelFactory
{
    public TabelaManutencaoViewModel Criar(TipoTabelaTributaria tipo) => new(tipo, service, atualizador, notificador);
}
