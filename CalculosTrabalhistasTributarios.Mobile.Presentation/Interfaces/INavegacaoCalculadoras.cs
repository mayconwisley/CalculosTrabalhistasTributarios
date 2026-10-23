using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

public interface INavegacaoCalculadoras
{
    Task AbrirAsync(TipoCalculadoraMobile calculadora);

    /// <summary>Abre o holerite ou a rescisão já com a quitação do banco de horas para conferência.</summary>
    Task AbrirAsync(TipoCalculadoraMobile calculadora, QuitacaoBancoHoras quitacao);
}
