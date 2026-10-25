using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class AtalhosDoIconeMobileTests
{
    [Fact]
    public void Sem_favoritas_usa_as_calculadoras_padrao()
    {
        var atalhos = AtalhosDoIconeMobile.Escolher([]);

        Assert.Equal(AtalhosDoIconeMobile.Padrao, atalhos.Select(cartao => cartao.Tipo));
        Assert.All(atalhos, cartao => Assert.False(string.IsNullOrWhiteSpace(cartao.Titulo)));
    }

    [Fact]
    public void Favoritas_vem_primeiro_na_ordem_marcada_sem_repetir_e_sem_tabelas()
    {
        var atalhos = AtalhosDoIconeMobile.Escolher(["Tabela.Inss", "Calculadora.Ferias", "Calculadora.Plr", "Calculadora.Inexistente", "Calculadora.Ferias"]);

        Assert.Equal([TipoCalculadoraMobile.Ferias, TipoCalculadoraMobile.Plr, TipoCalculadoraMobile.SimulacaoTributaria, TipoCalculadoraMobile.Rescisao],
            atalhos.Select(cartao => cartao.Tipo));
    }

    [Fact]
    public void Mais_de_quatro_favoritas_ficam_nas_quatro_primeiras()
    {
        var atalhos = AtalhosDoIconeMobile.Escolher(["Calculadora.Plr", "Calculadora.CltPj", "Calculadora.Holerite", "Calculadora.BancoHoras", "Calculadora.Rra"]);

        Assert.Equal(AtalhosDoIconeMobile.Limite, atalhos.Count);
        Assert.DoesNotContain(atalhos, cartao => cartao.Tipo == TipoCalculadoraMobile.Rra);
    }

    [Theory]
    [InlineData("Calculadora.Rescisao", TipoCalculadoraMobile.Rescisao)]
    [InlineData("Calculadora.99", null)]
    [InlineData("Tabela.Rescisao", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Id_do_atalho_volta_para_a_calculadora(string? id, TipoCalculadoraMobile? esperado)
    {
        Assert.Equal(esperado, AtalhosDoIconeMobile.Interpretar(id));
        if (esperado is { } tipo) Assert.Equal(id, AtalhosDoIconeMobile.Id(tipo));
    }
}
