using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculadoraBancoHoras = CalculosTrabalhistasTributarios.Domain.Trabalhista.CalculadoraBancoHoras;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class BancoHorasTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private static readonly DateOnly Fim = new(2026, 1, 31);

    [Fact]
    public void Ordena_movimentos_concilia_minutos_e_quita_saldo_no_fechamento()
    {
        var apuracao = CalculadoraBancoHoras.Calcular(Inicio, Fim, RegimeBancoHoras.MesmoMes,
            SituacaoBancoHoras.Fechamento, 2200m, 220m, 50m,
            [
                new(new DateOnly(2026, 1, 20), TipoLancamentoBancoHoras.Compensacao, 45, "Saída antecipada"),
                new(new DateOnly(2026, 1, 10), TipoLancamentoBancoHoras.Credito, 90, "Hora extra")
            ]).Sucesso();

        Assert.Equal(90, apuracao.MinutosCreditados);
        Assert.Equal(45, apuracao.MinutosCompensados);
        Assert.Equal(45, apuracao.SaldoMinutos);
        Assert.Equal([90, 45], apuracao.Movimentos.Select(item => item.SaldoAposLancamento));
        Assert.Equal(11.25m, apuracao.ValorAPagar);
    }

    [Fact]
    public void Compensacao_consume_creditos_antigos_e_quitacao_separa_adicionais()
    {
        // Hora normal R$ 10; 30 min restantes a 50% = R$ 7,50; 60 min a 100% = R$ 20,00.
        var apuracao = CalculadoraBancoHoras.Calcular(Inicio, Fim, RegimeBancoHoras.MesmoMes,
            SituacaoBancoHoras.Fechamento, 2200m, 220m, 50m,
            [
                new(Inicio, TipoLancamentoBancoHoras.Credito, 90, "primeiro", 50m),
                new(Inicio.AddDays(1), TipoLancamentoBancoHoras.Credito, 60, "segundo", 100m),
                new(Inicio.AddDays(2), TipoLancamentoBancoHoras.Compensacao, 60, "folga")
            ]).Sucesso();

        Assert.Equal(90, apuracao.SaldoCredor);
        Assert.Equal(27.50m, apuracao.ValorQuitacao);
        Assert.Equal([(50m, 30, 7.50m), (100m, 60, 20m)],
            apuracao.ParcelasQuitacao.Select(parcela => (parcela.Adicional, parcela.Minutos, parcela.Valor)));
    }

    [Fact]
    public void Acompanhamento_nao_paga_saldo_e_saldo_negativo_nao_vira_desconto()
    {
        var credito = new LancamentoBancoHoras(Inicio, TipoLancamentoBancoHoras.Credito, 60, "");
        var acompanhamento = CalculadoraBancoHoras.Calcular(Inicio, Fim, RegimeBancoHoras.MesmoMes,
            SituacaoBancoHoras.Acompanhamento, 2200m, 220m, 50m, [credito]).Sucesso();
        Assert.Equal(15m, acompanhamento.ValorQuitacao);
        Assert.Equal(0m, acompanhamento.ValorAPagar);
        Assert.Equal(0m, CalculadoraBancoHoras.Calcular(Inicio, Fim, RegimeBancoHoras.MesmoMes,
            SituacaoBancoHoras.Acompanhamento, 0m, 220m, 50m, [credito]).Sucesso().ValorQuitacao);
        Assert.True(CalculadoraBancoHoras.Calcular(Inicio, Fim, RegimeBancoHoras.MesmoMes,
            SituacaoBancoHoras.Rescisao, 0m, 220m, 50m, [credito]).Falhou);

        var negativo = CalculadoraBancoHoras.Calcular(Inicio, Fim, RegimeBancoHoras.MesmoMes,
            SituacaoBancoHoras.Rescisao, 2200m, 220m, 50m,
            [credito, new(Inicio.AddDays(1), TipoLancamentoBancoHoras.Compensacao, 120, "Folga")]).Sucesso();
        Assert.Equal(60, negativo.SaldoDevedor);
        Assert.Equal(0m, negativo.ValorAPagar);
    }

    [Fact]
    public void Prazo_do_regime_e_limite_diario_sao_validados()
    {
        var credito = new LancamentoBancoHoras(Inicio, TipoLancamentoBancoHoras.Credito, 60, "");
        Assert.True(CalculadoraBancoHoras.Calcular(Inicio, Inicio.AddMonths(1), RegimeBancoHoras.MesmoMes,
            SituacaoBancoHoras.Acompanhamento, 2200m, 220m, 50m, [credito]).Falhou);
        Assert.True(CalculadoraBancoHoras.Calcular(Inicio, Inicio.AddMonths(6).AddDays(1), RegimeBancoHoras.AcordoIndividualEscrito,
            SituacaoBancoHoras.Acompanhamento, 2200m, 220m, 50m, [credito]).Falhou);
        Assert.True(CalculadoraBancoHoras.Calcular(Inicio, Inicio.AddYears(1).AddDays(1), RegimeBancoHoras.AcordoColetivo,
            SituacaoBancoHoras.Acompanhamento, 2200m, 220m, 50m, [credito]).Falhou);
        Assert.True(CalculadoraBancoHoras.Calcular(Inicio, Fim, RegimeBancoHoras.MesmoMes,
            SituacaoBancoHoras.Acompanhamento, 2200m, 220m, 50m, [credito, credito with { Minutos = 61 }]).Falhou);
        Assert.True(CalculadoraBancoHoras.Calcular(Inicio, Fim, RegimeBancoHoras.MesmoMes,
            SituacaoBancoHoras.Acompanhamento, 2200m, 220m, 40m, [credito]).Falhou);
        Assert.True(CalculadoraBancoHoras.Calcular(new DateOnly(9999, 12, 1), DateOnly.MaxValue,
            RegimeBancoHoras.AcordoColetivo, SituacaoBancoHoras.Acompanhamento, 2200m, 220m, 50m,
            [new(new DateOnly(9999, 12, 1), TipoLancamentoBancoHoras.Credito, 60, "")]).Sucesso().SaldoCredor > 0);
    }

    [Fact]
    public void Formulario_reabre_lancamentos_e_horas_em_minutos()
    {
        var campo = new CampoBancoHorasViewModel { Inicio = "01/01/2026", Fim = "31/01/2026", Salario = "2.200,00" };
        campo.Lancamentos[0].Data = "10/01/2026";
        campo.Lancamentos[0].Horas = "1:30";
        campo.Lancamentos[0].Adicional = "100";
        campo.AdicionarCommand.Execute(null);
        campo.Lancamentos[1].Data = "20/01/2026";
        campo.Lancamentos[1].Tipo = LancamentoBancoHorasViewModel.Tipos[1];
        campo.Lancamentos[1].Horas = "0:45";

        var restaurado = new CampoBancoHorasViewModel();
        Assert.True(restaurado.Importar(campo.Exportar()));
        var dados = restaurado.Ler().Sucesso();
        Assert.Equal(90, dados.Lancamentos[0].Minutos);
        Assert.Equal(100m, dados.Lancamentos[0].Adicional);
        Assert.Equal(45, dados.Lancamentos[1].Minutos);
        Assert.Equal(TipoLancamentoBancoHoras.Compensacao, dados.Lancamentos[1].Tipo);
    }

    [Fact]
    public void Historico_antigo_sem_adicional_por_linha_usa_percentual_do_ciclo()
    {
        const string json = """
            {"Inicio":"01/01/2026","Fim":"31/01/2026","Regime":0,"Situacao":1,"Salario":"2.200,00","Divisor":"220","Adicional":"60","Lancamentos":[{"Data":"01/01/2026","Tipo":0,"Horas":"1:00","Descricao":""}]}
            """;
        var campo = new CampoBancoHorasViewModel();
        Assert.True(campo.Importar(json));
        var entrada = campo.Ler().Sucesso();
        Assert.Null(entrada.Lancamentos[0].Adicional);
        Assert.Equal(16m, CalculadoraBancoHoras.Calcular(entrada.Inicio, entrada.Fim, entrada.Regime,
            entrada.Situacao, entrada.Salario, entrada.Divisor, entrada.Adicional, entrada.Lancamentos).Sucesso().ValorQuitacao);
    }

    [Fact]
    public async Task Demonstrativo_na_rescisao_mostra_verba_e_nao_desconta_saldo_negativo()
    {
        var caso = new SimularBancoHorasUseCase();
        var request = new SimularBancoHorasRequest(Inicio, Fim, RegimeBancoHoras.MesmoMes,
            SituacaoBancoHoras.Rescisao, 2200m, 220m, 50m,
            [new(Inicio, TipoLancamentoBancoHoras.Credito, 60, "")]);
        var resultado = (await caso.ExecutarAsync(request, CancellationToken.None)).Sucesso();
        Assert.Equal(15m, resultado.Resultado);

        var negativo = (await caso.ExecutarAsync(request with
        {
            Lancamentos = [new(Inicio, TipoLancamentoBancoHoras.Compensacao, 60, "")]
        }, CancellationToken.None)).Sucesso();
        Assert.Equal(0m, negativo.Resultado);
        Assert.Empty(negativo.Descontos);
    }

    [Fact]
    public async Task Acompanhamento_sem_salario_informa_que_quitacao_nao_foi_calculada()
    {
        var resultado = (await new SimularBancoHorasUseCase().ExecutarAsync(new SimularBancoHorasRequest(
            Inicio, Fim, RegimeBancoHoras.MesmoMes, SituacaoBancoHoras.Acompanhamento, 0m, 220m, 50m,
            [new(Inicio, TipoLancamentoBancoHoras.Credito, 60, "")]), CancellationToken.None)).Sucesso();

        Assert.Equal("Não calculada", resultado.Destaques.Single(item => item.Rotulo == "Quitação estimada").Valor);
        Assert.Contains(resultado.Memoria[0].Formulas, item => item.Titulo == "Quitação estimada"
            && item.Formula.Contains("Não calculada"));
    }
}
