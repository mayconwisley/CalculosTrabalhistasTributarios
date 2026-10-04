using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface ITabelaManutencaoViewModelFactory
{
    TabelaManutencaoViewModel Criar(TipoTabelaTributaria tipo);
}
