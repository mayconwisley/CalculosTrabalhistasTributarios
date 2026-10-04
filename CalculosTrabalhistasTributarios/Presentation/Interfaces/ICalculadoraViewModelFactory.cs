using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface ICalculadoraViewModelFactory
{
    CalculadoraViewModel Criar(TipoCalculadora tipo);
}
