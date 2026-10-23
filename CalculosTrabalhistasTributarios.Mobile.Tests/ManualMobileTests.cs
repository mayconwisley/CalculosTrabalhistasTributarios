using CalculosTrabalhistasTributarios.Mobile.Presentation.Ajuda;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class ManualMobileTests
{
    [Fact]
    public void Toda_calculadora_abre_a_propria_secao_do_manual()
    {
        foreach (var tipo in Enum.GetValues<TipoCalculadoraMobile>())
        {
            var numero = ManualMobile.NumeroSecao(tipo);
            Assert.True(numero is not null, $"{tipo} sem seção no manual.");
            var blocos = ManualMobile.Secao(numero!);
            Assert.True(blocos.Count > 1, $"{tipo}: seção {numero} vazia.");
            Assert.Equal(TipoBlocoManualMobile.Titulo, blocos[0].Tipo);
            Assert.StartsWith(numero + " ", blocos[0].Texto);
        }
    }

    [Fact]
    public void Secao_inclui_subsecoes_e_para_na_proxima_do_mesmo_nivel()
    {
        var decimoTerceiro = ManualMobile.Secao("8.3");

        Assert.Equal("8.3 13º salário", decimoTerceiro[0].Texto);
        Assert.Contains(decimoTerceiro, bloco => bloco.Tipo == TipoBlocoManualMobile.Subtitulo && bloco.Texto.StartsWith("8.3.1 ", StringComparison.Ordinal));
        Assert.DoesNotContain(decimoTerceiro, bloco => bloco.Texto.StartsWith("8.4 ", StringComparison.Ordinal));
    }

    [Fact]
    public void Tabela_vira_itens_sem_cabecalho_e_links_perdem_a_marcacao()
    {
        var ferias = ManualMobile.Secao("8.4");

        Assert.Contains(ferias, bloco => bloco.Tipo == TipoBlocoManualMobile.Item
            && bloco.Texto.StartsWith("**Competência do pagamento**: ", StringComparison.Ordinal));
        Assert.DoesNotContain(ferias, bloco => bloco.Texto.StartsWith("**Campo**", StringComparison.Ordinal));
        Assert.DoesNotContain(ferias, bloco => bloco.Texto.Contains("](", StringComparison.Ordinal) || bloco.Texto.Contains("![", StringComparison.Ordinal));
        Assert.Empty(ManualMobile.Secao("99"));
    }
}
