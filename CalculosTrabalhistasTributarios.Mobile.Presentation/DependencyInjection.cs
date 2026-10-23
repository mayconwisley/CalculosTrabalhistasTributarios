using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;
using Microsoft.Extensions.DependencyInjection;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services) => services
        .AddTransient<SaqueAniversarioViewModel>();
}
