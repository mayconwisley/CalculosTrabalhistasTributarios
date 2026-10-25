using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Resultado;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class ResumoTextoDemonstrativoTests
{
    [Fact]
    public void Resumo_traz_destaques_totais_com_os_rotulos_do_calculo_e_o_aviso()
    {
        var demonstrativo = new DemonstrativoDto("Férias", "Período 01/2026",
            [new("Férias com 1/3", "R$ 4.000,00", "30 dias"), new("Dias", "30", "")],
            [new("Férias", "30 dias", 3000m), new("1/3 constitucional", "", 1000m)],
            [new("INSS", "", 330.52m)],
            [new("FGTS", "8%", 320m)], [], [],
            RotuloProventos: "Total bruto", RotuloResultado: "Valor a receber");

        var texto = ResumoTextoDemonstrativo.Montar(demonstrativo).Replace(" ", " ");

        Assert.StartsWith("Férias" + Environment.NewLine + "Período 01/2026", texto);
        Assert.Contains("Férias com 1/3: R$ 4.000,00 (30 dias)", texto);
        Assert.Contains("Dias: 30" + Environment.NewLine, texto);
        Assert.Contains("Total bruto: R$ 4.000,00", texto);
        Assert.Contains("Descontos: R$ 330,52", texto);
        Assert.Contains("Valor a receber: R$ 3.669,48", texto);
        Assert.DoesNotContain("FGTS", texto);
        Assert.EndsWith(AvisoSimulacao.Titulo + Environment.NewLine + AvisoSimulacao.Orientacao, texto);
    }

    [Fact]
    public void Comparacao_sem_verbas_mostra_as_colunas_e_nao_inventa_totais_zerados()
    {
        var demonstrativo = new DemonstrativoDto("Comparação de regimes", "", [], [], [], [], [], [],
            Comparativo: new TabelaComparativaDto("Imposto anual", ["Simplificada", "Completa"],
                [new("IR devido", ["R$ 1.000,00", "R$ 800,00"], true)]));

        var texto = ResumoTextoDemonstrativo.Montar(demonstrativo);

        Assert.Contains("Imposto anual" + Environment.NewLine + "IR devido: Simplificada R$ 1.000,00 | Completa R$ 800,00", texto);
        Assert.DoesNotContain("Descontos", texto);
        Assert.DoesNotContain("Líquido a receber", texto);
    }
}
