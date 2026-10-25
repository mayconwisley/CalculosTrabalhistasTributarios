using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>Exporta o histórico para um arquivo e o restaura depois, por exemplo ao reinstalar o aplicativo.</summary>
public interface ICopiaSegurancaHistoricoUseCase
{
    /// <summary>Todos os cálculos salvos; falha quando o histórico está vazio.</summary>
    Task<Result<ArquivoCopiaSegurancaDto>> ExportarAsync(string origem, DateTime agora, CancellationToken cancellationToken);

    /// <summary>Acrescenta os cálculos da cópia ao histórico, sem apagar nem substituir os que já existem.</summary>
    Task<Result<ResumoRestauracaoHistoricoDto>> RestaurarAsync(string conteudo, string origem, CancellationToken cancellationToken);
}
