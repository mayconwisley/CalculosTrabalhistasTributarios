using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

/// <summary>Gera o documento local e abre a escolha de aplicativo; não confirma o envio ao destinatário.</summary>
public interface ICompartilhamentoPdf
{
    Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken);
}
