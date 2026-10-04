using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>
/// Exportação dos resultados para o Excel (.xlsx), com os valores como números, para o usuário somar, filtrar e montar as
/// próprias contas. Tem o mesmo conteúdo dos relatórios em PDF.
/// </summary>
public interface IPlanilhaService
{
    Task GerarImpostoAsync(SimulacaoImpostoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken);
    Task GerarPensaoAsync(SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada, string caminhoArquivo, CancellationToken cancellationToken);
    Task GerarDemonstrativoAsync(DemonstrativoDto demonstrativo, string caminhoArquivo, CancellationToken cancellationToken);
    Task GerarPensaoAtrasoAsync(SimulacaoPensaoAtrasoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken);
    Task GerarEstabilidadeAsync(SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada, string caminhoArquivo, CancellationToken cancellationToken);
    Task GerarJornadaAsync(SimulacaoJornadaDto apuracao, string caminhoArquivo, CancellationToken cancellationToken);
    Task GerarDebitoJudicialAsync(SimulacaoDebitoJudicialDto simulacao, string caminhoArquivo, CancellationToken cancellationToken);
}
