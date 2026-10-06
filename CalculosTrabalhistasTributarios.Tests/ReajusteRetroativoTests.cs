using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculadoraReajusteRetroativo = CalculosTrabalhistasTributarios.Domain.Trabalhista.CalculadoraReajusteRetroativo;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class ReajusteRetroativoTests
{
    private static readonly DateOnly Janeiro = new(2026, 1, 1);

    [Fact]
    public void Apura_meses_com_antecipacao_e_promocao_sem_repetir_a_mesma_diferenca()
    {
        var resultado = CalculadoraReajusteRetroativo.Calcular(
        [
            new(Janeiro, TipoParcelaReajuste.Salario, 1000m, 1150m),
            new(Janeiro.AddMonths(1), TipoParcelaReajuste.Salario, 1100m, 1150m),
            new(Janeiro.AddMonths(2), TipoParcelaReajuste.Salario, 1400m, 1610m)
        ]).Sucesso();

        Assert.Equal([150m, 50m, 210m], resultado.Parcelas.Select(item => item.Diferenca));
        Assert.Equal(410m, resultado.Salario);
        Assert.Equal(32.80m, resultado.Fgts);
    }

    [Fact]
    public void Calcula_decimo_terceiro_por_avos_e_ferias_gozadas_com_terco()
    {
        var resultado = CalculadoraReajusteRetroativo.Calcular(
        [
            new(Janeiro, TipoParcelaReajuste.Salario, 2000m, 2200m),
            new(Janeiro.AddMonths(1), TipoParcelaReajuste.DecimoTerceiro, 2000m, 2200m, 6),
            new(Janeiro.AddMonths(2), TipoParcelaReajuste.FeriasGozadas, 2000m, 2200m, 15)
        ]).Sucesso();

        Assert.Equal(100m, resultado.DecimoTerceiro);
        Assert.Equal(133.34m, resultado.Ferias);
        Assert.Equal(433.34m, resultado.TotalBruto);
        Assert.Equal(34.67m, resultado.Fgts);
    }

    [Fact]
    public void Rejeita_duplicidade_mensal_valor_ja_pago_maior_e_quantidade_invalida()
    {
        var mes = new ParcelaReajusteRetroativo(Janeiro, TipoParcelaReajuste.Salario, 1000m, 1100m);
        Assert.True(CalculadoraReajusteRetroativo.Calcular([mes, mes]).Falhou);
        Assert.True(CalculadoraReajusteRetroativo.Calcular([mes with { BasePaga = 1200m }]).Falhou);
        Assert.True(CalculadoraReajusteRetroativo.Calcular([mes, new(Janeiro, TipoParcelaReajuste.DecimoTerceiro, 1000m, 1100m, 13)]).Falhou);
        Assert.True(CalculadoraReajusteRetroativo.Calcular([mes, new(Janeiro, TipoParcelaReajuste.FeriasGozadas, 1000m, 1100m, 0)]).Falhou);
        Assert.True(CalculadoraReajusteRetroativo.Calcular([mes, new(Janeiro, TipoParcelaReajuste.DecimoTerceiro, 0m, 0m, 12)]).Falhou);
    }

    [Fact]
    public void Formulario_gera_meses_editaveis_e_restauraveis_pelo_historico()
    {
        var campo = new CampoReajusteRetroativoViewModel
        {
            Inicio = "01/2026", Fim = "03/2026", SalarioAnterior = "1.000,00", Percentual = "15"
        };
        Assert.True(campo.Gerar());
        Assert.Equal(3, campo.Meses.Count);
        campo.Meses[1].BasePaga = "1.100,00";
        campo.AdicionarDecimoTerceiroCommand.Execute(null);
        campo.Reflexos[0].BasePaga = "1.000,00";
        campo.Reflexos[0].BaseDevida = "1.150,00";
        campo.Reflexos[0].Quantidade = "6";

        var restaurado = new CampoReajusteRetroativoViewModel();
        Assert.True(restaurado.Importar(campo.Exportar()));
        Assert.Equal("1.100,00", restaurado.Meses[1].BasePaga);
        Assert.Equal("6", restaurado.Reflexos[0].Quantidade);
        Assert.Equal(4, restaurado.Ler().Sucesso().Count);
    }

    [Fact]
    public async Task Demonstrativo_distingue_total_bruto_do_fgts_do_empregador()
    {
        var useCase = new SimularReajusteRetroativoUseCase();
        var demonstrativo = (await useCase.ExecutarAsync(new SimularReajusteRetroativoRequest(
            [new(Janeiro, TipoParcelaReajuste.Salario, 1000m, 1150m)]), CancellationToken.None)).Sucesso();

        Assert.Equal(150m, demonstrativo.Resultado);
        Assert.Equal(12m, demonstrativo.Informativos.Single().Valor);
        Assert.Empty(demonstrativo.Descontos);
        Assert.Contains(demonstrativo.Observacoes, texto => texto.Contains("INSS, IRRF"));
    }
}
