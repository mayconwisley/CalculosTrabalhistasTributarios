using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

public sealed class NavegadorCalculadoras(Func<TipoCalculadoraMobile, Page> criarPagina) : INavegacaoCalculadoras
{
    private INavigation? _navegacao;
    public void Configurar(INavigation navegacao) => _navegacao = navegacao;
    public Task AbrirAsync(TipoCalculadoraMobile calculadora) => Navegacao.PushAsync(criarPagina(calculadora));

    public Task AbrirAsync(TipoCalculadoraMobile calculadora, QuitacaoBancoHoras quitacao)
    {
        var pagina = criarPagina(calculadora);
        if (pagina.BindingContext is not CalculadoraTrabalhistaViewModel calculadoraDestino)
            throw new ArgumentOutOfRangeException(nameof(calculadora), "A calculadora não recebe a quitação do banco de horas.");
        calculadoraDestino.ReceberQuitacao(quitacao);
        return Navegacao.PushAsync(pagina);
    }

    private INavigation Navegacao => _navegacao ?? throw new InvalidOperationException("Navegação não configurada.");
}
