using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

public interface IRelatorioPdfService
{
    Task GerarRelatorioImpostoAsync(SimulacaoImpostoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken);
    Task GerarRelatorioPensaoAsync(SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada, bool incluirDetalhes, string caminhoArquivo, CancellationToken cancellationToken);
    Task GerarDemonstrativoAsync(DemonstrativoDto demonstrativo, string caminhoArquivo, CancellationToken cancellationToken);
    Task GerarRelatorioPensaoAtrasoAsync(SimulacaoPensaoAtrasoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken);
    Task GerarRelatorioDebitoJudicialAsync(SimulacaoDebitoJudicialDto simulacao, string caminhoArquivo, CancellationToken cancellationToken);
    Task GerarRelatorioEstabilidadeAsync(SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada, string caminhoArquivo, CancellationToken cancellationToken);
}
