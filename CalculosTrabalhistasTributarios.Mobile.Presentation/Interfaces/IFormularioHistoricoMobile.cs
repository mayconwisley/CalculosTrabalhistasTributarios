using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

public interface IFormularioHistoricoMobile
{
    string Titulo { get; }
    long? HistoricoId { get; set; }
    DadosFormularioMobile ExportarDados();
    Result TentarImportarDados(DadosFormularioMobile dados);
}
