using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class EdicaoNumeroMobileTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("0,00")]
    [InlineData("0,0")]
    public void FocoLimpaZeroNumerico(string texto) =>
        Assert.Equal(string.Empty, EdicaoNumeroMobile.AoReceberFoco(texto));

    [Theory]
    [InlineData("2")]
    [InlineData("1,5")]
    [InlineData("3.500")]
    [InlineData("-1")]
    [InlineData("inválido")]
    public void FocoPreservaValorPreenchidoOuInvalido(string texto) =>
        Assert.Equal(texto, EdicaoNumeroMobile.AoReceberFoco(texto));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SaidaVaziaRestauraZero(string? texto) =>
        Assert.Equal("0", EdicaoNumeroMobile.AoPerderFoco(texto));

    [Theory]
    [InlineData("12")]
    [InlineData("1,25")]
    [InlineData("inválido")]
    public void SaidaPreservaTextoSemFormatarComoMoeda(string texto) =>
        Assert.Equal(texto, EdicaoNumeroMobile.AoPerderFoco(texto));
}
