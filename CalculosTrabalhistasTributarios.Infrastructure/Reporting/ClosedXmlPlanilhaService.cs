using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting;

/// <summary>Adaptador ClosedXML da porta de planilhas: cada planilha tem a sua classe em <c>Reporting/Planilha</c>.</summary>
public sealed class ClosedXmlPlanilhaService : IPlanilhaService
{
    public Task GerarImpostoAsync(SimulacaoImpostoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) =>
        PlanilhaImposto.GerarImpostoAsync(simulacao, caminhoArquivo, cancellationToken);

    public Task GerarPensaoAsync(SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada, string caminhoArquivo, CancellationToken cancellationToken) =>
        PlanilhaPensao.GerarPensaoAsync(simulacao, entrada, caminhoArquivo, cancellationToken);

    public Task GerarDemonstrativoAsync(DemonstrativoDto demonstrativo, string caminhoArquivo, CancellationToken cancellationToken) =>
        PlanilhaDemonstrativo.GerarDemonstrativoAsync(demonstrativo, caminhoArquivo, cancellationToken);

    public Task GerarPensaoAtrasoAsync(SimulacaoPensaoAtrasoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) =>
        PlanilhaPensaoAtraso.GerarPensaoAtrasoAsync(simulacao, caminhoArquivo, cancellationToken);

    public Task GerarEstabilidadeAsync(SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada, string caminhoArquivo, CancellationToken cancellationToken) =>
        PlanilhaEstabilidade.GerarEstabilidadeAsync(simulacao, entrada, caminhoArquivo, cancellationToken);

    public Task GerarJornadaAsync(SimulacaoJornadaDto apuracao, string caminhoArquivo, CancellationToken cancellationToken) =>
        PlanilhaJornada.GerarJornadaAsync(apuracao, caminhoArquivo, cancellationToken);

    public Task GerarDebitoJudicialAsync(SimulacaoDebitoJudicialDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) =>
        PlanilhaDebitoJudicial.GerarDebitoJudicialAsync(simulacao, caminhoArquivo, cancellationToken);
}
