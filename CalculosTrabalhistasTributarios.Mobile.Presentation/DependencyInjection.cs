using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;
using Microsoft.Extensions.DependencyInjection;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Tributacao;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services) => services
        .AddTransient<SaqueAniversarioViewModel>()
        .AddTransient<SimulacaoTributariaViewModel>()
        .AddTransient<InicioViewModel>();
}
