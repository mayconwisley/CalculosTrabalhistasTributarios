using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class CatalogoCalculadorasMobileTests
{
    [Fact]
    public void Cada_calculadora_tem_um_unico_cartao_com_texto_e_formulario_valido()
    {
        var cartoes = CatalogoCalculadorasMobile.Cartoes.ToList();
        Assert.Equal(Enum.GetValues<TipoCalculadoraMobile>().Order(), cartoes.Select(cartao => cartao.Tipo).Order());
        Assert.All(CatalogoCalculadorasMobile.Grupos, grupo => Assert.NotEmpty(grupo.Cartoes));
        Assert.All(cartoes, cartao =>
        {
            Assert.False(string.IsNullOrWhiteSpace(cartao.Titulo));
            Assert.False(string.IsNullOrWhiteSpace(cartao.Descricao));
            Assert.False(string.IsNullOrWhiteSpace(cartao.TextoBotao));
            if (cartao.ViewModel is not null)
                Assert.True(cartao.ViewModel.IsSubclassOf(typeof(CalculadoraTrabalhistaViewModel)), cartao.ViewModel.Name);
        });
        Assert.Equal(cartoes.Count, cartoes.Select(cartao => cartao.IdAutomacao).Distinct().Count());
    }

    [Fact]
    public void Paginas_proprias_nao_usam_a_tela_comum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogoCalculadorasMobile.TipoViewModel(TipoCalculadoraMobile.SimulacaoTributaria));
        Assert.Equal(typeof(HoleriteMobileViewModel), CatalogoCalculadorasMobile.TipoViewModel(TipoCalculadoraMobile.Holerite));
    }
}
