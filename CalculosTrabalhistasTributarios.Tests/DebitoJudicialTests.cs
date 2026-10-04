using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using System.Globalization;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class DebitoJudicialTests
{
    // Séries oficiais digitadas para o recálculo independente, em % ao mês.
    private static readonly Dictionary<(int, int), decimal> Ipca = Serie((2024, 1, "0.42 0.83 0.16 0.38 0.46 0.21 0.38 -0.02 0.44 0.56 0.39 0.52"),
        (2025, 1, "0.16 1.31 0.56 0.43 0.26 0.24 0.26 -0.11 0.48 0.09 0.18 0.33"), (2026, 1, "0.33 0.70 0.88 0.67 0.58 0.16 0.07 -0.32"));
    private static readonly Dictionary<(int, int), decimal> Inpc = Serie((2024, 1, "0.57 0.81 0.19 0.37 0.46 0.25 0.26 -0.14"));
    private static readonly Dictionary<(int, int), decimal> IpcaE = Serie((2024, 1, "0.31 0.78 0.36"));
    private static readonly Dictionary<(int, int), decimal> Tr = Serie((2024, 1, "0.0875 0.0079 0.0331"));
    private static readonly Dictionary<(int, int), decimal> Selic = Serie((2024, 1, "0.97 0.8 0.83 0.89 0.83 0.79 0.91 0.87"));
    private static readonly Dictionary<(int, int), decimal> TaxaLegal = Serie((2024, 8, "0.605306 0.676227 0.704241 0.385874 0.171924"),
        (2025, 1, "0.589427 0.902209 0 0.321969 0.6232 0.775982 0.83488 0.942622 1.305984 0.736394 1.093764 0.851001"),
        (2026, 1, "0.96751 0.962232 0.155714 0.768672 0.198293 0.450641 0.706607 1.154527 1.499119 0.37902"));

    private static readonly DateOnly Calculo = new(2026, 10, 10);

    private static async Task<Result<SimulacaoDebitoJudicialDto>> ResultadoAsync(NaturezaDebito natureza, DateOnly? ajuizamento = null, InicioJurosCivel inicio = InicioJurosCivel.Vencimento,
        DateOnly? citacao = null, IndiceCivelAnterior indice = IndiceCivelAnterior.Inpc, AcrescimosPenhora acrescimos = AcrescimosPenhora.Nenhum) =>
        await new SimularDebitoJudicialUseCase(await Ambiente.IndicesAsync()).CalcularAsync(new SimularDebitoJudicialRequest(
            natureza, [new ParcelaDebito("Parcela", new DateOnly(2024, 1, 10), 1000m)], Calculo, ajuizamento, inicio, citacao, indice, acrescimos), default);

    private static Task<SimulacaoDebitoJudicialDto> CalcularAsync(NaturezaDebito natureza, DateOnly? ajuizamento = null, InicioJurosCivel inicio = InicioJurosCivel.Vencimento,
        DateOnly? citacao = null, IndiceCivelAnterior indice = IndiceCivelAnterior.Inpc, AcrescimosPenhora acrescimos = AcrescimosPenhora.Nenhum) =>
        ResultadoAsync(natureza, ajuizamento, inicio, citacao, indice, acrescimos).Sucesso();

    [Fact]
    public async Task Trabalhista_em_tres_fases()
    {
        var parcela = (await CalcularAsync(NaturezaDebito.Trabalhista, new DateOnly(2024, 3, 15))).Parcelas.Single();

        // IPCA-E de janeiro e fevereiro; TR de 11/01 a 14/03; Selic de 15/03 a 29/08/2024; IPCA de 09/2024 até o último
        // publicado (08/2026) e taxa legal de 30/08/2024 a 10/10/2026.
        var fator = Fator(IpcaE, new(2024, 1, 1), new(2024, 3, 1)) * Fator(Ipca, new(2024, 9, 1), new(2026, 10, 1));
        var selic = Taxa(Selic, new(2024, 3, 14), new(2024, 8, 29));
        var juros = Taxa(Tr, new(2024, 1, 10), new(2024, 3, 14)) + Taxa(TaxaLegal, new(2024, 8, 29), Calculo);
        var atualizado = Math.Round(1000m * fator * (1m + selic / 100m), 2, MidpointRounding.AwayFromZero);

        Assert.Equal(Math.Round(fator, 6), parcela.FatorCorrecao);
        Assert.Equal(selic, parcela.PercentualSelic);
        Assert.Equal(atualizado, parcela.Atualizado);
        Assert.Equal(juros, parcela.PercentualJuros);
        Assert.Equal(Math.Round(atualizado * juros / 100m, 2, MidpointRounding.AwayFromZero), parcela.Juros);
    }

    [Fact]
    public async Task Trabalhista_sem_ajuizamento_fica_todo_na_fase_pre_judicial()
    {
        var resultado = await CalcularAsync(NaturezaDebito.Trabalhista);
        // IPCA-E de 01/2024 a 09/2026 e juros só pela TR, sem Selic nem taxa legal.
        var parcela = resultado.Parcelas.Single();
        Assert.Equal(0m, parcela.PercentualSelic);
        Assert.True(parcela.PercentualJuros is > 0m and < 10m);
        Assert.Empty(resultado.Observacoes);
    }

    [Fact]
    public async Task Civel_com_juros_desde_o_vencimento_usa_a_selic_ate_29_08_2024()
    {
        var parcela = (await CalcularAsync(NaturezaDebito.Civel)).Parcelas.Single();
        var selic = Taxa(Selic, new(2024, 1, 10), new(2024, 8, 29));
        Assert.Equal(Math.Round(Fator(Ipca, new(2024, 9, 1), new(2026, 10, 1)), 6), parcela.FatorCorrecao);
        Assert.Equal(selic, parcela.PercentualSelic);
        Assert.Equal(Taxa(TaxaLegal, new(2024, 8, 29), Calculo), parcela.PercentualJuros);
    }

    [Fact]
    public async Task Civel_com_juros_desde_a_citacao_depois_da_lei_corrige_pelo_indice_e_depois_pelo_ipca()
    {
        var parcela = (await CalcularAsync(NaturezaDebito.Civel, inicio: InicioJurosCivel.Citacao, citacao: new DateOnly(2024, 10, 15))).Parcelas.Single();
        // INPC de 01 a 07/2024, IPCA desde 08/2024, sem Selic, e taxa legal só depois da citação.
        Assert.Equal(Math.Round(Fator(Inpc, new(2024, 1, 1), new(2024, 8, 1)) * Fator(Ipca, new(2024, 8, 1), new(2026, 10, 1)), 6), parcela.FatorCorrecao);
        Assert.Equal(0m, parcela.PercentualSelic);
        Assert.Equal(Taxa(TaxaLegal, new(2024, 10, 15), Calculo), parcela.PercentualJuros);
    }

    [Fact]
    public async Task Multa_e_honorarios_so_no_civel()
    {
        var civel = await CalcularAsync(NaturezaDebito.Civel, acrescimos: AcrescimosPenhora.MultaEHonorarios);
        Assert.Equal(Math.Round(civel.Total * .1m, 2, MidpointRounding.AwayFromZero), civel.Multa);
        Assert.Equal(civel.Multa, civel.Honorarios);
        var trabalhista = await CalcularAsync(NaturezaDebito.Trabalhista, new DateOnly(2024, 3, 15), acrescimos: AcrescimosPenhora.MultaEHonorarios);
        Assert.False(trabalhista.TemAcrescimos);
    }

    [Fact]
    public async Task Exige_a_citacao_quando_os_juros_comecam_nela() =>
        await ResultadoAsync(NaturezaDebito.Civel, inicio: InicioJurosCivel.Citacao).Falha();

    /// <summary>Produto das variações dos meses de <paramref name="inicio"/> até o anterior a <paramref name="fim"/>; mês sem índice fica de fora.</summary>
    private static decimal Fator(Dictionary<(int, int), decimal> serie, DateOnly inicio, DateOnly fim)
    {
        var fator = 1m;
        for (var mes = inicio; mes < fim; mes = mes.AddMonths(1))
            if (serie.TryGetValue((mes.Year, mes.Month), out var variacao))
                fator *= 1m + variacao / 100m;
        return fator;
    }

    /// <summary>Taxa proporcional aos dias, dos dias seguintes a <paramref name="inicio"/> até <paramref name="fim"/>, mês a mês.</summary>
    private static decimal Taxa(Dictionary<(int, int), decimal> serie, DateOnly inicio, DateOnly fim)
    {
        var total = 0m;
        for (var dia = inicio.AddDays(1); dia <= fim;)
        {
            var dias = DateTime.DaysInMonth(dia.Year, dia.Month);
            var ultimo = new DateOnly(dia.Year, dia.Month, dias);
            var ate = fim < ultimo ? fim : ultimo;
            total += Math.Round(serie[(dia.Year, dia.Month)] * (ate.DayNumber - dia.DayNumber + 1) / dias, 6);
            dia = ate.AddDays(1);
        }
        return total;
    }

    private static Dictionary<(int, int), decimal> Serie(params (int Ano, int MesInicial, string Valores)[] anos)
    {
        var serie = new Dictionary<(int, int), decimal>();
        foreach (var (ano, mesInicial, valores) in anos)
        {
            var mes = new DateOnly(ano, mesInicial, 1);
            foreach (var valor in valores.Split(' '))
            {
                serie[(mes.Year, mes.Month)] = decimal.Parse(valor, CultureInfo.InvariantCulture);
                mes = mes.AddMonths(1);
            }
        }
        return serie;
    }
}
