using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Quitação antecipada do Crédito do Trabalhador pela taxa do contrato (Resolução CMN 5.004/2022, art. 7º).</summary>
public class QuitacaoCreditoTests
{
    // 1.000,00 em 3 parcelas a 10% a.m.: parcela 402,11; saldos 697,89 e 365,57; última parcela 365,57 + 36,56 = 402,13.
    private static ApuracaoCreditoTrabalhador Credito() => CalculadoraCreditoTrabalhador.Calcular(
        new EntradaCreditoTrabalhador(ObjetivoCreditoTrabalhador.ValorDesejado, 1000m, 3, 10m, 0m, 0m, 0m, 5000m, 0m)).Sucesso();

    [Fact]
    public void Quitacao_abate_os_juros_futuros_pela_taxa_do_contrato()
    {
        var credito = Credito();
        Assert.Equal((402.11m, 402.13m), (credito.Parcela, credito.UltimaParcela));

        var quitacao = CalculadoraCreditoTrabalhador.Quitacao(credito, 10m, 3, 1).Sucesso();

        Assert.Equal(2, quitacao.ParcelasRestantes);
        Assert.Equal(804.24m, quitacao.ValorDasRestantes);
        Assert.Equal(697.89m, quitacao.SaldoParaQuitar);
        Assert.Equal(106.35m, quitacao.JurosAbatidos);
        // Depois de duas parcelas, falta só a última: 365,57 hoje contra 402,13 no vencimento.
        Assert.Equal(365.57m, CalculadoraCreditoTrabalhador.Quitacao(credito, 10m, 3, 2).Sucesso().SaldoParaQuitar);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Quitacao_exige_parcelas_pagas_dentro_do_prazo(int pagas) =>
        Assert.Contains("de 1 a 2 parcelas", CalculadoraCreditoTrabalhador.Quitacao(Credito(), 10m, 3, pagas).Erro.Mensagem);
}
