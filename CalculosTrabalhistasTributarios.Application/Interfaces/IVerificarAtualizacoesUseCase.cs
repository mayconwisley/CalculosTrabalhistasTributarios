using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>Avisos exibidos ao abrir o aplicativo: versão nova publicada e tabelas do ano ainda não cadastradas.</summary>
public interface IVerificarAtualizacoesUseCase
{
    /// <param name="versaoAtual">Versão em execução, como "1.6.1".</param>
    /// <param name="consultarVersao">Falso quando o usuário desligou a consulta de novas versões.</param>
    Task<IReadOnlyList<AvisoAtualizacaoDto>> ExecutarAsync(string versaoAtual, bool consultarVersao, DateOnly hoje, CancellationToken cancellationToken);
}
