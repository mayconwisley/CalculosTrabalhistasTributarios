using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Atualizacao;
using CalculosTrabalhistasTributarios.Infrastructure.Diagnostico;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using CalculosTrabalhistasTributarios.Infrastructure.Reporting;
using CalculosTrabalhistasTributarios.Infrastructure.Tabelas;
using CalculosTrabalhistasTributarios.Infrastructure.Tributacao;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using CalculosTrabalhistasTributarios.Infrastructure.Local;

namespace CalculosTrabalhistasTributarios.Infrastructure;

public static class DependencyInjection
{
    // O banco fica na pasta do executável, e não na pasta de trabalho, que muda conforme o atalho ou o terminal que abriu o app.
    private static readonly string CaminhoBanco = Path.Combine(AppContext.BaseDirectory, "BancoDados", "calculoIrrf.db");

    // A manutenção e a atualização das tabelas ficam em Infrastructure.Tabelas, compartilhada com o celular.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) => services
        .AddInfrastructureLocal(CaminhoBanco)
        .AddAtualizacaoTabelas()
        .AddSingleton<IConsultaCotacoesTrabalhoExterior, ConsultaCotacoesTrabalhoExterior>()
        .AddSingleton<IHistoricoCalculos, SqliteHistoricoCalculos>()
        .AddSingleton<IRelatorioPdfService, QuestPdfRelatorioPdfService>()
        .AddSingleton<IPlanilhaService, ClosedXmlPlanilhaService>()
        .AddSingleton<IRegistroDeErros, ArquivoRegistroDeErros>()
        .AddSingleton<IConsultaVersaoPublicada, ConsultaVersaoGitHub>()
        .AddSingleton<IBaixadorDeAtualizacao, BaixadorDeAtualizacaoGitHub>();
}
