using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Atualizacao;
using CalculosTrabalhistasTributarios.Infrastructure.Diagnostico;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using CalculosTrabalhistasTributarios.Infrastructure.Reporting;
using CalculosTrabalhistasTributarios.Infrastructure.Tributacao;
using CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace CalculosTrabalhistasTributarios.Infrastructure;

public static class DependencyInjection
{
    // O banco fica na pasta do executável, e não na pasta de trabalho, que muda conforme o atalho ou o terminal que abriu o app.
    private static readonly string CaminhoBanco = Path.Combine(AppContext.BaseDirectory, "BancoDados", "calculoIrrf.db");

    // As fontes de cada tabela aparecem nas mensagens ao usuário na ordem em que estão registradas.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) => services
        .AddSingleton(new BancoTributario($"Data Source={CaminhoBanco}"))
        .AddSingleton<Func<HttpClient>>(static () => CriarHttpClient())
        .AddSingleton<IConsultaCotacoesTrabalhoExterior, ConsultaCotacoesTrabalhoExterior>()
        .AddSingleton<SqliteTributacaoConsulta>()
        .AddSingleton<ITributacaoConsulta>(provider => provider.GetRequiredService<SqliteTributacaoConsulta>())
        .AddSingleton<ICacheTabelasTributarias>(provider => provider.GetRequiredService<SqliteTributacaoConsulta>())
        .AddScoped<ITabelaTributariaService, SqliteTabelaTributariaService>()
        .AddSingleton<IIndicesEconomicos, SqliteIndicesEconomicos>()
        .AddSingleton<IHistoricoCalculos, SqliteHistoricoCalculos>()
        .AddSingleton<IRelatorioPdfService, QuestPdfRelatorioPdfService>()
        .AddSingleton<IPlanilhaService, ClosedXmlPlanilhaService>()
        .AddSingleton<IRegistroDeErros, ArquivoRegistroDeErros>()
        .AddSingleton<IConsultaVersaoPublicada, ConsultaVersaoGitHub>()
        .AddSingleton<IBaixadorDeAtualizacao, BaixadorDeAtualizacaoGitHub>()
        .AddSingleton<IFonteTabela<TabelaIrrfPublicada>, FonteIrrfReceitaFederal>()
        .AddSingleton<IFonteTabela<TabelaIrrfPublicada>, FonteIrrfDebit>()
        .AddSingleton<IFonteTabela<TabelaIrrfPublicada>, FonteIrrfContabeis>()
        .AddSingleton<IFonteTabela<TabelaInssPublicada>, FonteInssGovBr>()
        .AddSingleton<IFonteTabela<TabelaInssPublicada>, FonteInssDebit>()
        .AddSingleton<IFonteTabela<TabelaInssPublicada>, FonteInssContabeis>()
        .AddSingleton<IFonteTabela<TabelaPlrPublicada>, FontePlrReceitaFederal>()
        .AddSingleton<IFonteTabela<TabelaSalarioFamiliaPublicada>, FonteSalarioFamiliaGovBr>()
        .AddSingleton<IFonteTabela<TabelaSalarioFamiliaPublicada>, FonteSalarioFamiliaDebit>()
        .AddSingleton<IFonteTabela<TabelaSalarioFamiliaPublicada>, FonteSalarioFamiliaContabeis>()
        .AddSingleton<IFonteTabela<SalarioMinimoPublicado>, FonteSalarioMinimoGovBr>()
        .AddSingleton<IFonteTabela<SalarioMinimoPublicado>, FonteSalarioMinimoDebit>()
        .AddSingleton<IFonteTabela<SalarioMinimoPublicado>, FonteSalarioMinimoContabeis>()
        .AddSingleton<IFonteTabela<TabelaSeguroDesempregoPublicada>, FonteSeguroDesempregoGovBr>()
        .AddSingleton<IFonteTabela<TabelaSeguroDesempregoPublicada>, FonteSeguroDesempregoDebit>()
        .AddSingleton<IFonteTabela<TabelaSeguroDesempregoPublicada>, FonteSeguroDesempregoIdinheiro>()
        .AddSingleton<IFonteTabela<DescontoMinimoPublicado>, FonteDescontoMinimoPlanalto>()
        .AddSingleton<IFonteIndice>(new FonteIndiceIbge(TipoTabelaTributaria.Inpc))
        .AddSingleton<IFonteIndice>(new FonteIndiceIbge(TipoTabelaTributaria.Ipca))
        .AddSingleton<IFonteIndice, FonteTaxaLegalBancoCentral>()
        .AddSingleton<IFonteIndice>(new FonteIndiceVri(TipoTabelaTributaria.Inpc))
        .AddSingleton<IFonteIndice>(new FonteIndiceVri(TipoTabelaTributaria.Ipca))
        .AddSingleton<IFonteIndice, FonteTaxaLegalCajud>()
        .AddSingleton<IFonteIndice>(new FonteIndiceIbge(TipoTabelaTributaria.IpcaE))
        .AddSingleton<IFonteIndice>(new FonteSerieBancoCentral(TipoTabelaTributaria.Selic, 4390, "https://www.bcb.gov.br/controleinflacao/historicotaxasjuros"))
        .AddSingleton<IFonteIndice>(new FonteSerieIpeadata(TipoTabelaTributaria.Selic, "BM12_TJOVER12"))
        .AddSingleton<IFonteIndice>(new FonteSerieBancoCentral(TipoTabelaTributaria.Tr, 7811, "https://www.bcb.gov.br/estabilidadefinanceira/historicotaxasreferenciais"))
        .AddSingleton<IFonteIndice>(new FonteSerieIpeadata(TipoTabelaTributaria.Tr, "BM12_TJTR12"))
        .AddScoped<IAtualizadorTabelaIrrf, AtualizadorTabelaIrrf>()
        .AddScoped<IAtualizadorTabelaInss, AtualizadorTabelaInss>()
        .AddScoped<AtualizadorTabelaPlr>()
        .AddScoped<AtualizadorSalarioFamilia>()
        .AddScoped<AtualizadorSalarioMinimo>()
        .AddScoped<AtualizadorSeguroDesemprego>()
        .AddScoped<AtualizadorDescontoMinimo>()
        .AddScoped<AtualizadorIndices>()
        .AddScoped<IAtualizadorTabelas, AtualizadorTabelas>()
        .AddScoped<IInicializadorBancoTributario, InicializadorBancoTributario>();

    // Identifica o aplicativo para os sites consultados e limita a espera: uma fonte fora do ar não pode travar a atualização.
    // "Mozilla/5.0 (compatible; ...)" é o formato dos robôs que se identificam; o Planalto recusa a conexão sem ele.
    private static HttpClient CriarHttpClient()
    {
        var versao = typeof(DependencyInjection).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue($"(compatible; CalculosTrabalhistasTributarios/{versao}; +https://github.com/mayconwisley/CalculosTrabalhistasTributarios)"));
        return httpClient;
    }
}
