using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class DemonstrativoComparacaoHistoricoTests
{
    [Fact]
    public void Pdf_da_comparacao_leva_as_linhas_na_ordem_com_os_nomes_e_a_premissa_do_recalculo()
    {
        LinhaResultadoComparadoViewModel[] linhas =
        [
            new("Destaques", "Líquido", "R$ 3.000,00", "R$ 3.200,00", "R$ 200,00"),
            new("Destaques", "Dias", "30", "30", "")
        ];

        var demonstrativo = DemonstrativoComparacaoHistorico.Montar("Março", "Abril", linhas, new DateTime(2026, 4, 10, 9, 5, 0));

        Assert.False(demonstrativo.TemVerbas);
        Assert.Equal("Primeiro: Março · Segundo: Abril", demonstrativo.Referencia);
        var comparativo = Assert.IsType<CalculosTrabalhistasTributarios.Application.DTOs.TabelaComparativaDto>(demonstrativo.Comparativo);
        Assert.Equal(["Primeiro", "Segundo", "Diferença"], comparativo.Colunas);
        Assert.Collection(comparativo.Linhas,
            linha =>
            {
                Assert.Equal("Destaques · Líquido", linha.Descricao);
                Assert.Equal(["R$ 3.000,00", "R$ 3.200,00", "R$ 200,00"], linha.Valores);
                Assert.True(linha.Destaque);
            },
            linha => Assert.False(linha.Destaque));
        Assert.Contains(DemonstrativoComparacaoHistorico.ObservacaoRecalculo, demonstrativo.Observacoes);
        Assert.Contains("Comparação gerada em 10/04/2026 09:05.", demonstrativo.Observacoes);
    }
}
