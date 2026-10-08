using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface IComparacaoHistoricoService
{
    void Mostrar(CalculoSalvoDto primeiro, CalculoSalvoDto segundo);
}
