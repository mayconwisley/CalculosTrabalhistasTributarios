using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>Atualização pela internet de todas as tabelas de uma vez, usada na abertura do aplicativo.</summary>
public interface IAtualizarTodasTabelasUseCase
{
    /// <summary>Quantidade de tabelas consultadas, para a barra de progresso começar com o total certo.</summary>
    int Total { get; }

    Task<IReadOnlyList<AtualizacaoDeTabela>> ExecutarAsync(IProgress<ProgressoAtualizacaoTabelas>? progresso, CancellationToken cancellationToken);
}
