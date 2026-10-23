using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

public sealed class NavegadorCalculadoras(Func<TipoCalculadoraMobile, Page> criarPagina) : INavegacaoCalculadoras
{
    private INavigation? _navegacao;
    public void Configurar(INavigation navegacao) => _navegacao = navegacao;
    public Task AbrirAsync(TipoCalculadoraMobile calculadora) =>
        (_navegacao ?? throw new InvalidOperationException("Navegação não configurada.")).PushAsync(criarPagina(calculadora));
}
