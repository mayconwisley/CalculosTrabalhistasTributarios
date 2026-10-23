using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

public interface INavegacaoTransferenciasMobile
{
    Task AbrirComDadosAsync(TipoCalculadoraMobile calculadora, DadosFormularioMobile dados);
}
