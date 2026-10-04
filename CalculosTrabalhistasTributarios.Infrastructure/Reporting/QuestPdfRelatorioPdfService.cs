using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting;

/// <summary>Adaptador QuestPDF da porta de relatórios: cada relatório tem a sua classe em <c>Reporting/Pdf</c>.</summary>
public sealed class QuestPdfRelatorioPdfService : IRelatorioPdfService
{
    public Task GerarRelatorioImpostoAsync(SimulacaoImpostoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) =>
        RelatorioPdfImposto.GerarRelatorioImpostoAsync(simulacao, caminhoArquivo, cancellationToken);

    public Task GerarRelatorioPensaoAsync(SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada, bool incluirDetalhes, string caminhoArquivo, CancellationToken cancellationToken) =>
        RelatorioPdfPensao.GerarRelatorioPensaoAsync(simulacao, entrada, incluirDetalhes, caminhoArquivo, cancellationToken);

    public Task GerarRelatorioPensaoAtrasoAsync(SimulacaoPensaoAtrasoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) =>
        RelatorioPdfPensaoAtraso.GerarRelatorioPensaoAtrasoAsync(simulacao, caminhoArquivo, cancellationToken);

    public Task GerarRelatorioDebitoJudicialAsync(SimulacaoDebitoJudicialDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) =>
        RelatorioPdfDebitoJudicial.GerarRelatorioDebitoJudicialAsync(simulacao, caminhoArquivo, cancellationToken);

    public Task GerarRelatorioEstabilidadeAsync(SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada, string caminhoArquivo, CancellationToken cancellationToken) =>
        RelatorioPdfEstabilidade.GerarRelatorioEstabilidadeAsync(simulacao, entrada, caminhoArquivo, cancellationToken);

    public Task GerarDemonstrativoAsync(DemonstrativoDto demonstrativo, string caminhoArquivo, CancellationToken cancellationToken) =>
        RelatorioPdfDemonstrativo.GerarDemonstrativoAsync(demonstrativo, caminhoArquivo, cancellationToken);
}
