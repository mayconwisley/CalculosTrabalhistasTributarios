using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

public sealed class NavegadorCalculadoras(Func<TipoCalculadoraMobile, Page> criarPagina) : INavegacaoCalculadoras, INavegacaoTransferenciasMobile
{
    private INavigation? _navegacao;
    private FlyoutPage? _menu;
    public void Configurar(INavigation navegacao, FlyoutPage menu) { _navegacao = navegacao; _menu = menu; }
    public Task AbrirAsync(TipoCalculadoraMobile calculadora)
    {
        if (_menu is not null) _menu.IsPresented = false;
        return Navegacao.PushAsync(criarPagina(calculadora));
    }

    public Task AbrirAsync(TipoCalculadoraMobile calculadora, QuitacaoBancoHoras quitacao)
    {
        var pagina = criarPagina(calculadora);
        pagina.SetValue(ApoioFormularioMobile.RecebeuDadosProperty, true);
        if (pagina.BindingContext is not CalculadoraTrabalhistaViewModel calculadoraDestino)
            throw new ArgumentOutOfRangeException(nameof(calculadora), "A calculadora não recebe a quitação do banco de horas.");
        calculadoraDestino.ReceberQuitacao(quitacao);
        if (_menu is not null) _menu.IsPresented = false;
        return Navegacao.PushAsync(pagina);
    }

    public Task AbrirComDadosAsync(TipoCalculadoraMobile calculadora, DadosFormularioMobile dados)
    {
        var pagina = criarPagina(calculadora);
        pagina.SetValue(ApoioFormularioMobile.RecebeuDadosProperty, true);
        if (pagina.BindingContext is not IFormularioHistoricoMobile formulario)
            throw new ArgumentOutOfRangeException(nameof(calculadora), "A calculadora não recebe dados transferidos.");
        var importacao = formulario.TentarImportarDados(dados);
        if (importacao.Falhou) throw new InvalidOperationException(importacao.Erro.Mensagem);
        if (_menu is not null) _menu.IsPresented = false;
        return Navegacao.PushAsync(pagina);
    }

    private INavigation Navegacao => _navegacao ?? throw new InvalidOperationException("Navegação não configurada.");
}
