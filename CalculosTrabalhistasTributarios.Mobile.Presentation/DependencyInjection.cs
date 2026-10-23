using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;
using Microsoft.Extensions.DependencyInjection;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services) => services
        .AddTransient<SaqueAniversarioViewModel>()
        .AddTransient<SimulacaoTributariaViewModel>()
        .AddTransient<DecimoTerceiroMobileViewModel>()
        .AddTransient<FeriasMobileViewModel>()
        .AddTransient<HorasExtrasMobileViewModel>()
        .AddTransient<RescisaoMobileViewModel>()
        .AddTransient<InicioViewModel>();
}
