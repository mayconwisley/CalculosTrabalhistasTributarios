namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

/// <summary>Notifica apenas mudanças dos dados persistidos, incluindo linhas e importações.</summary>
public interface IFormularioEditavelMobile : IFormularioHistoricoMobile
{
    event EventHandler? FormularioAlterado;
}
