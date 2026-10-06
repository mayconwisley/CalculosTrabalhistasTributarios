using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace CalculosTrabalhistasTributarios.Application;

public static class DependencyInjection
{
    /// <summary>Casos de uso da aplicação; as portas que eles usam são registradas pela infraestrutura.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddScoped<ISimularImpostoUseCase, SimularImpostoUseCase>()
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
        .AddScoped<ISimularDemonstrativoUseCase<SimularSaqueAniversarioRequest>, SimularSaqueAniversarioUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularAbonoSalarialRequest>, SimularAbonoSalarialUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularCarneLeaoRequest>, SimularCarneLeaoUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularGanhoCapitalRequest>, SimularGanhoCapitalUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularEstagioRequest>, SimularEstagioUseCase>()
        .AddScoped<ISimularDemonstrativoUseCase<SimularIntermitenteRequest>, SimularIntermitenteUseCase>()
        .AddScoped<IVerificarAtualizacoesUseCase, VerificarAtualizacoesUseCase>();
}
