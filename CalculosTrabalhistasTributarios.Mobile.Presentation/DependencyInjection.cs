using CalculosTrabalhistasTributarios.Mobile.Presentation.SaqueAniversario;
using Microsoft.Extensions.DependencyInjection;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Abertura;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Configuracoes;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Tributacao;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services
            .AddTransient<SaqueAniversarioViewModel>()
            .AddTransient<SimulacaoTributariaViewModel>()
            // A tela inicial e o menu lateral da mesma janela compartilham a busca e as favoritas.
            .AddScoped<InicioViewModel>()
            .AddSingleton<SituacaoAtualizacaoTabelas>()
            .AddTransient<AtualizacaoTabelasViewModel>()
            .AddTransient<ConfiguracoesMobileViewModel>();
        // Formulários novos por página: cada abertura começa vazia.
        foreach (var cartao in CatalogoCalculadorasMobile.Cartoes.Where(cartao => cartao.ViewModel is not null))
            services.AddTransient(cartao.ViewModel!);
        return services;
    }
}
