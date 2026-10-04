using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>Baixa o instalador de uma versão publicada e confere a integridade dele antes de entregá-lo.</summary>
public interface IBaixadorDeAtualizacao
{
    /// <param name="progresso">Percentual baixado, de 0 a 100.</param>
    /// <returns>Caminho do instalador conferido, pronto para executar.</returns>
    Task<Result<string>> BaixarAsync(VersaoPublicada versao, IProgress<int>? progresso, CancellationToken cancellationToken);
}
