using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

public interface INavegacaoCalculadoras
{
    Task AbrirAsync(TipoCalculadoraMobile calculadora);
}
