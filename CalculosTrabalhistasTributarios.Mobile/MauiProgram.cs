using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Mobile.Presentation;
using CalculosTrabalhistasTributarios.Mobile.Features.SaqueAniversario;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSaqueAniversario().AddPresentation();
        builder.Services.AddTransient<SaqueAniversarioPage>();
        builder.Services.AddSingleton<Func<Window>>(provider => () =>
        {
            var scope = provider.CreateScope();
            try
            {
                var page = scope.ServiceProvider.GetRequiredService<SaqueAniversarioPage>();
                var window = new Window(new NavigationPage(page));
                window.Destroying += (_, _) => scope.Dispose();
                return window;
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        });
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
