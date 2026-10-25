using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

/// <summary>Navegação, confirmação e edição de nome dependentes da plataforma.</summary>
public interface IInteracaoHistoricoMobile
{
    Task AbrirAsync(ResumoCalculoSalvoDto salvo);
    Task CompararAsync(ResumoCalculoSalvoDto primeiro, ResumoCalculoSalvoDto segundo);
    /// <summary>Recalcula o formulário salvo com as tabelas atuais e compartilha o PDF, sem abrir a calculadora.</summary>
    Task CompartilharPdfAsync(ResumoCalculoSalvoDto salvo);
    Task DuplicarAsync(ResumoCalculoSalvoDto salvo);
    Task RenomearAsync(ResumoCalculoSalvoDto salvo);
    Task ExcluirAsync(ResumoCalculoSalvoDto salvo);
}
