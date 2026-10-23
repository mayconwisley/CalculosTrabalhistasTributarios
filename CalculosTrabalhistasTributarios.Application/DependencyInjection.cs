using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace CalculosTrabalhistasTributarios.Application;

public static class DependencyInjection
{
    /// <summary>Casos de uso da aplicação; as portas que eles usam são registradas pela infraestrutura.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddCalculadoras()
        .AddScoped<IVerificarAtualizacoesUseCase, VerificarAtualizacoesUseCase>()
        .AddAtualizacaoTodasTabelas();

    /// <summary>Atualização de todas as tabelas da abertura; a porta <see cref="IAtualizadorTabelas"/> vem de Infrastructure.Tabelas.</summary>
    public static IServiceCollection AddAtualizacaoTodasTabelas(this IServiceCollection services) => services
        .AddScoped<IAtualizarTodasTabelasUseCase, AtualizarTodasTabelasUseCase>();

    /// <summary>Calculadoras do desktop e do aplicativo móvel; dependem só das tabelas e dos índices do banco local.</summary>
    public static IServiceCollection AddCalculadoras(this IServiceCollection services) => services
        .AddSimulacaoTributaria()
        .AddScoped<ISimularPensaoUseCase, SimularPensaoUseCase>()
        .AddScoped<ISimularPensaoAtrasoUseCase, SimularPensaoAtrasoUseCase>()
        .AddScoped<ISimularEstabilidadeUseCase, SimularEstabilidadeUseCase>()
        .AddSingleton<IApurarJornadaUseCase, ApurarJornadaUseCase>()
        .AddScoped<ISimularDebitoJudicialUseCase, SimularDebitoJudicialUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularSalarioPeloLiquidoRequest>, SimularSalarioPeloLiquidoUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularDecimoTerceiroRequest>, SimularDecimoTerceiroUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularFeriasRequest>, SimularFeriasUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularHorasExtrasRequest>, SimularHorasExtrasUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularComissoesRequest>, SimularComissoesUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularReajusteRetroativoRequest>, SimularReajusteRetroativoUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularMediaVerbasVariaveisRequest>, SimularMediaVerbasVariaveisUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularBancoHorasRequest>, SimularBancoHorasUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularMultiplosVinculosInssRequest>, SimularMultiplosVinculosInssUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularRescisaoRequest>, SimularRescisaoUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularCustoFuncionarioRequest>, SimularCustoFuncionarioUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularProLaboreRequest>, SimularProLaboreUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularPlrRequest>, SimularPlrUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularSalarioFamiliaRequest>, SimularSalarioFamiliaUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularAdicionaisRequest>, SimularAdicionaisUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularRevisaoPensaoRequest>, SimularRevisaoPensaoUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularSeguroDesempregoRequest>, SimularSeguroDesempregoUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularCltPjRequest>, SimularCltPjUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularHoleriteRequest>, SimularHoleriteUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularIrpfAnualRequest>, SimularIrpfAnualUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularDividendosRequest>, SimularDividendosUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularTributoAtrasoRequest>, SimularTributoAtrasoUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularCorrecaoValorRequest>, SimularCorrecaoValorUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularDomesticoRequest>, SimularDomesticoUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularAfastamentoRequest>, SimularAfastamentoUseCase>()
        .AddSaqueAniversario()
        .AddScoped<ISimularDemonstrativoUseCase<SimularAbonoSalarialRequest>, SimularAbonoSalarialUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularCarneLeaoRequest>, SimularCarneLeaoUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularTrabalhoExteriorRequest>, SimularTrabalhoExteriorUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularCreditoTrabalhadorRequest>, SimularCreditoTrabalhadorUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularEmprestimoPessoalRequest>, SimularEmprestimoPessoalUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularGanhoCapitalRequest>, SimularGanhoCapitalUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularEstagioRequest>, SimularEstagioUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularIntermitenteRequest>, SimularIntermitenteUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularRraRequest>, SimularRraUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularSimplesNacionalRequest>, SimularSimplesNacionalUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularComplementoDecimoTerceiroRequest>, SimularComplementoDecimoTerceiroUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularConferenciaFgtsRequest>, SimularConferenciaFgtsUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularSobreavisoRequest>, SimularSobreavisoUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularTransferenciaRequest>, SimularTransferenciaUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularGorjetasRequest>, SimularGorjetasUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularContribuicaoSeguradoRequest>, SimularContribuicaoSeguradoUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularResgatePrevidenciaRequest>, SimularResgatePrevidenciaUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularAposentadoriaRequest>, SimularAposentadoriaUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularPensaoMorteRequest>, SimularPensaoMorteUseCase>();

    /// <summary>Registra a simulação autônoma usada também pelo aplicativo móvel, sem exigir banco ou relatórios.</summary>
    public static IServiceCollection AddSaqueAniversario(this IServiceCollection services) => services
        .AddScoped<ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest>, SimularSaqueAniversarioUseCase>();

    public static IServiceCollection AddSimulacaoTributaria(this IServiceCollection services) => services
        .AddScoped<ISimularDemonstrativoUseCase<SimularImpostoRequest>, SimularImpostoUseCase>();
}
