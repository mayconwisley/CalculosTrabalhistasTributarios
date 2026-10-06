using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculadoraMediaVerbasVariaveis = CalculosTrabalhistasTributarios.Domain.Trabalhista.CalculadoraMediaVerbasVariaveis;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class MediaVerbasVariaveisTests
{
    private static readonly DateOnly Janeiro = new(2026, 1, 1);

    [Fact]
    public void Mes_zerado_permanece_no_divisor_e_verbas_sao_separadas()
    {
        var media = CalculadoraMediaVerbasVariaveis.Calcular(
        [
            new(Janeiro, 300m, 50m, 100m, 0m, 0m),
            new(Janeiro.AddMonths(1), 0m, 0m, 0m, 0m, 0m),
            new(Janeiro.AddMonths(2), 600m, 100m, 200m, 60m, 30m)
        ], 3).Sucesso();

        Assert.Equal(300m, media.MediaComissoes);
        Assert.Equal(50m, media.MediaDsr);
        Assert.Equal(100m, media.MediaHorasExtras);
        Assert.Equal(20m, media.MediaAdicionais);
        Assert.Equal(10m, media.MediaOutras);
        Assert.Equal(480m, media.MediaTotal);
        Assert.Equal(720m, CalculadoraMediaVerbasVariaveis.Calcular(media.Meses, 2).Sucesso().MediaTotal);
    }

    [Fact]
    public void Rejeita_competencia_duplicada_valor_negativo_e_divisor_invalido()
    {
        var mes = new VerbasVariaveisDoMes(Janeiro, 100m, 0m, 0m, 0m, 0m);
        Assert.True(CalculadoraMediaVerbasVariaveis.Calcular([mes, mes], 2).Falhou);
        Assert.True(CalculadoraMediaVerbasVariaveis.Calcular([mes with { Dsr = -1m }], 1).Falhou);
        Assert.True(CalculadoraMediaVerbasVariaveis.Calcular([mes], 0).Falhou);
        Assert.True(CalculadoraMediaVerbasVariaveis.Calcular([mes with { Comissoes = 1.001m }], 1).Falhou);
    }

    [Fact]
    public void Formulario_gera_meses_e_reabre_valores_do_historico()
    {
        var campo = new CampoMediaVerbasVariaveisViewModel { Inicio = "01/2026", Fim = "03/2026" };
        Assert.True(campo.Gerar());
        campo.Meses[0].Comissoes = "300,00";
        campo.Meses[2].Dsr = "50,00";
        var restaurado = new CampoMediaVerbasVariaveisViewModel();

        Assert.True(restaurado.Importar(campo.Exportar()));
        Assert.Equal("300,00", restaurado.Meses[0].Comissoes);
        Assert.Equal("50,00", restaurado.Meses[2].Dsr);
        Assert.Equal(3, restaurado.Ler().Sucesso().Divisor);
    }

    [Fact]
    public async Task Demonstrativo_usa_media_total_como_resultado()
    {
        var demonstrativo = (await new SimularMediaVerbasVariaveisUseCase().ExecutarAsync(
            new SimularMediaVerbasVariaveisRequest(
                [new(Janeiro, 0.01m, 0.01m, 0m, 0m, 0m), new(Janeiro.AddMonths(1), 0m, 0m, 0m, 0m, 0m),
                    new(Janeiro.AddMonths(2), 0m, 0m, 0m, 0m, 0m)], 3), CancellationToken.None)).Sucesso();

        Assert.Equal(0.01m, demonstrativo.Resultado);
        Assert.Equal(demonstrativo.Resultado.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),
            demonstrativo.Destaques[0].Valor);
    }
}
