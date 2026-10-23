using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Mobile.Features.Configuracoes;
using CalculosTrabalhistasTributarios.Mobile.Features.Tabelas;
using CalculosTrabalhistasTributarios.Mobile.Features.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Tabelas;
using Microsoft.Extensions.DependencyInjection;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

public sealed class NavegadorMenu(IServiceProvider services) : INavegacaoMenu
{
    private INavigation? _navegacao;
    private FlyoutPage? _menu;

    public void Configurar(INavigation navegacao, FlyoutPage menu)
    {
        _navegacao = navegacao;
        _menu = menu;
    }

    public async Task AbrirTabelaAsync(TipoTabelaTributaria tabela)
    {
        var vm = ActivatorUtilities.CreateInstance<TabelaMobileViewModel>(services, tabela);
        await AbrirAsync(new TabelaPage(vm));
    }

    public Task AbrirConfiguracoesAsync() => AbrirAsync(services.GetRequiredService<ConfiguracoesPage>());
    public Task AbrirHistoricoAsync() => AbrirAsync(services.GetRequiredService<HistoricoPage>());
    public Task AbrirPaginaAsync(Page page) => AbrirAsync(page);

    public async Task VoltarAoInicioAsync()
    {
        _menu!.IsPresented = false;
        await _navegacao!.PopToRootAsync();
    }

    private async Task AbrirAsync(Page page)
    {
        _menu!.IsPresented = false;
        await _navegacao!.PushAsync(page);
    }
}
