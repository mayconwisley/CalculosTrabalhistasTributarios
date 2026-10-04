using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Presentation;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class TabelasTests
{
    [Theory]
    [InlineData("2015-06", 788.00)]
    [InlineData("2016-06", 880.00)]
    [InlineData("2020-01", 1039.00)]
    [InlineData("2020-02", 1045.00)]
    [InlineData("2023-04", 1302.00)]
    [InlineData("2023-05", 1320.00)]
    [InlineData("2026-10", 1621.00)]
    public async Task Salario_minimo_de_cada_competencia(string mes, decimal valor) =>
        Assert.Equal(valor, await (await Ambiente.ConsultaAsync()).ObterSalarioMinimoAsync(DateOnly.ParseExact(mes + "-01", "yyyy-MM-dd"), default));

    [Theory]
    [InlineData("2023-04", null)]
    [InlineData("2023-05", 528.00)]
    [InlineData("2024-01", 528.00)]
    [InlineData("2024-02", 564.80)]
    [InlineData("2025-04", 564.80)]
    [InlineData("2025-05", 607.20)]
    [InlineData("2026-10", 607.20)]
    public async Task Desconto_simplificado_muda_na_vigencia_certa(string mes, double? valor)
    {
        var perfil = await (await Ambiente.ConsultaAsync()).ObterPerfilAsync(DateOnly.ParseExact(mes + "-01", "yyyy-MM-dd"), default).Sucesso();
        // Antes de 05/2023 a tabela guarda zero, e o cálculo trata a modalidade como inexistente.
        Assert.Equal(valor is null ? 0m : (decimal)valor, perfil.DeducaoSimplificada);
        Assert.Equal(10m, perfil.DescontoMinimo);
        Assert.Equal(189.59m, perfil.DeducaoPorDependente);
    }

    [Fact]
    public async Task Taxa_legal_vem_do_banco_central_desde_agosto_de_2024()
    {
        var taxas = await (await Ambiente.IndicesAsync()).ObterAsync(IndiceEconomico.TaxaLegal, default);
        Assert.Equal(new DateOnly(2024, 8, 1), taxas.Keys.Min());
        Assert.Equal(0.605306m, taxas[new DateOnly(2024, 8, 1)]);
        Assert.Equal(0m, taxas[new DateOnly(2025, 3, 1)]);
        Assert.Equal(1.499119m, taxas[new DateOnly(2026, 9, 1)]);
        Assert.DoesNotContain(taxas.Values, taxa => taxa < 0m);
    }

    [Fact]
    public async Task Salario_familia_e_reducao_de_2026()
    {
        var perfil = await (await Ambiente.ConsultaAsync()).ObterPerfilAsync(new DateOnly(2026, 10, 1), default).Sucesso();
        Assert.Equal((1980.38m, 67.54m), (perfil.FaixasSalarioFamilia![0].LimiteRemuneracao, perfil.FaixasSalarioFamilia[0].Cota));
        Assert.Equal([(5000m, 0m, 312.89m), (7350m, 0.133145m, 978.62m)], perfil.ReducoesMensaisIrrf.Select(regra => (regra.LimiteRendimentos, regra.Multiplicador, regra.ValorBase)));
    }

    [Fact]
    public async Task Competencia_antes_das_tabelas_explica_desde_quando_ha_dados()
    {
        var erro = await (await Ambiente.ConsultaAsync()).ObterPerfilAsync(new DateOnly(2016, 6, 1), default).Falha();
        Assert.Contains("as tabelas começam em 01/2017", erro.Mensagem);
    }
}

public class RegrasTrabalhistasTests
{
    [Theory]
    [InlineData("2025-03-10", "2026-03-09", 30)]
    [InlineData("2025-03-10", "2026-03-10", 33)]
    [InlineData("2016-03-10", "2026-10-20", 60)]
    [InlineData("1990-01-01", "2026-10-20", 90)]
    public void Aviso_previo_proporcional(string admissao, string desligamento, int dias) =>
        Assert.Equal(dias, RegrasTrabalhistas.DiasDeAvisoPrevio(DateOnly.Parse(admissao), DateOnly.Parse(desligamento)));

    [Theory]
    [InlineData("2026-01-17", "2026-12-31", 12)]
    [InlineData("2026-01-18", "2026-12-31", 11)]
    [InlineData("2026-01-17", "2026-03-14", 2)]
    [InlineData("2026-01-17", "2026-03-15", 3)]
    public void Avos_de_13o_contam_meses_com_15_dias_ou_mais(string inicio, string fim, int avos) =>
        Assert.Equal(avos, RegrasTrabalhistas.AvosDecimoTerceiro(DateOnly.Parse(inicio), DateOnly.Parse(fim), 2026));

    [Theory]
    [InlineData(5, 30)]
    [InlineData(6, 24)]
    [InlineData(14, 24)]
    [InlineData(23, 18)]
    [InlineData(32, 12)]
    [InlineData(33, 0)]
    public void Dias_de_ferias_pelas_faltas(int faltas, int dias) => Assert.Equal(dias, RegrasTrabalhistas.DiasDeFeriasPorFaltas(faltas));

    [Fact]
    public void Dsr_conta_domingos_e_feriados() =>
        Assert.Equal((26, 5), RegrasTrabalhistas.DiasParaDsr(new DateOnly(2026, 10, 1), 1).Sucesso());

    [Fact]
    public void Inss_trunca_e_irrf_arredonda()
    {
        Assert.Equal(121.57m, CalculadoraTributacao.Truncar(121.575m));
        Assert.Equal(121.58m, CalculadoraTributacao.Arredondar(121.575m));
        var faixas = new[] { new FaixaTributaria(1, 1621m, 7.5m), new FaixaTributaria(2, 2902.84m, 9m), new FaixaTributaria(3, 4354.27m, 12m), new FaixaTributaria(4, 8475.55m, 14m) };
        Assert.Equal(988.07m, CalculadoraInss.CalcularDetalhes(new DateOnly(2026, 1, 1), 8475.55m, faixas).Sum(faixa => faixa.Imposto));
    }
}

public class LeituraNumericaTests
{
    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("62.5", 62.5)]
    [InlineData("3.500", 3500)]
    [InlineData("3.500,00", 3500)]
    [InlineData("2200.50", 2200.50)]
    [InlineData("1.234.567", 1234567)]
    [InlineData("0.125", 0.125)]
    [InlineData("-0.32", -0.32)]
    [InlineData("1,5", 1.5)]
    [InlineData(" 7,25 ", 7.25)]
    [InlineData("1.000,5", 1000.5)]
    public void Le_numeros_com_ponto_ou_virgula(string texto, double esperado)
    {
        Assert.True(LeituraNumerica.TentarLer(texto, out var valor));
        Assert.Equal((decimal)esperado, valor);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("")]
    public void Recusa_textos_que_nao_sao_numero(string texto) => Assert.False(LeituraNumerica.TentarLer(texto, out _));
}
