using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Domain.Previdencia.PensaoMorte;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Pensão por morte da EC 103/2019 com valores conferidos à mão; mínimo de 1.621,00 e teto de 8.475,55.</summary>
public class PensaoMorteTests
{
    private static readonly DateOnly Obito = new(2026, 10, 13);

    private static EntradaPensaoMorte Entrada(SituacaoInstituidor situacao = SituacaoInstituidor.NaoAposentado, decimal aposentadoria = 0m, decimal media = 6000m,
        int meses = 360, CausaObito causa = CausaObito.Comum, bool conjuge = true, int idadeConjuge = 40, bool uniao = true, int outros = 1,
        bool invalido = false, decimal beneficioConjuge = 0m, DateOnly? obito = null) =>
        new(obito ?? Obito, situacao, aposentadoria, media, SexoSegurado.Masculino, meses, causa, conjuge, idadeConjuge, uniao, outros, invalido, beneficioConjuge, 1621m, 8475.55m);

    [Fact]
    public void Nao_aposentado_com_conjuge_e_um_filho()
    {
        // Base: 6.000 x (60% + 2 x 10 anos acima de 20) = 4.800; cota 50% + 2 x 10% = 70%: 3.360; 1.680 por dependente.
        var p = CalculadoraPensaoMorte.Calcular(Entrada()).Sucesso();

        Assert.Equal((4800m, 80m, 70m, 3360m, 1680m), (p.Base, p.CoeficienteBase, p.Cota, p.Pensao, p.CotaPorDependente));
        // Cônjuge com 40 anos: 15 anos, até 13/10/2041.
        Assert.Equal((180, new DateOnly(2041, 10, 13), false), (p.DuracaoMeses, p.FimConjuge, p.Vitalicia));
    }

    [Fact]
    public void Redutor_na_acumulacao_com_beneficio_proprio()
    {
        // A aposentadoria própria de 3.000 é mais vantajosa: a cota de 1.680 fica com 1.621 + 59 x 60% = 1.656,40.
        var p = CalculadoraPensaoMorte.Calcular(Entrada(beneficioConjuge: 3000m)).Sucesso();
        Assert.Equal((true, 1656.40m, 4656.40m), (p.ReduzidoPensao, p.ValorReduzido, p.TotalConjuge));
    }

    [Fact]
    public void Redutor_por_faixas_de_salario_minimo()
    {
        // 5.000 com mínimo de 1.000: 1.000 + 1.000 x 60% + 1.000 x 40% + 1.000 x 20% + 1.000 x 10% = 2.300.
        Assert.Equal(2300m, CalculadoraPensaoMorte.Redutor(5000m, 1000m));
        Assert.Equal(800m, CalculadoraPensaoMorte.Redutor(800m, 1000m));
    }

    [Fact]
    public void Pensao_abaixo_do_minimo_sobe_ao_minimo_e_e_vitalicia_aos_45()
    {
        // Aposentadoria de 2.000, só o cônjuge: 60% = 1.200, elevada a 1.621.
        var p = CalculadoraPensaoMorte.Calcular(Entrada(SituacaoInstituidor.Aposentado, 2000m, outros: 0, idadeConjuge: 50)).Sucesso();
        Assert.Equal((1200m, 1621m, true, true), (p.Calculada, p.Pensao, p.ElevadaAoMinimo, p.Vitalicia));
    }

    [Fact]
    public void Acidente_do_trabalho_dispensa_a_carencia_e_usa_100_por_cento_da_media()
    {
        // 2 anos de contribuição e 1 ano de união, mas óbito por acidente do trabalho: base 3.000; 60% = 1.800; cônjuge de 25 anos: 6 anos.
        var p = CalculadoraPensaoMorte.Calcular(Entrada(media: 3000m, meses: 24, causa: CausaObito.AcidenteTrabalho, idadeConjuge: 25, uniao: false, outros: 0)).Sucesso();
        Assert.Equal((3000m, 1800m, 72), (p.Base, p.Pensao, p.DuracaoMeses));
    }

    [Fact]
    public void Sem_18_contribuicoes_a_cota_do_conjuge_dura_4_meses()
    {
        var p = CalculadoraPensaoMorte.Calcular(Entrada(meses: 12)).Sucesso();
        Assert.Equal((4, new DateOnly(2027, 2, 13)), (p.DuracaoMeses, p.FimConjuge));
    }

    [Fact]
    public void Dependente_invalido_leva_a_100_por_cento_e_a_cota_tem_limite()
    {
        Assert.Equal((100m, 4800m), (CalculadoraPensaoMorte.Calcular(Entrada(invalido: true)).Sucesso().Cota, CalculadoraPensaoMorte.Calcular(Entrada(invalido: true)).Sucesso().Pensao));
        Assert.Equal(100m, CalculadoraPensaoMorte.Calcular(Entrada(outros: 6)).Sucesso().Cota);   // 50% + 7 x 10%, limitado a 100%
    }

    // Limites de cada faixa conferidos no texto oficial: IN PRES/INSS 128/2022, art. 375, § 8º (Portaria ME 424/2020, óbitos
    // desde 01/01/2021), e Lei 8.213/1991, art. 77, § 2º, V, "c" (óbitos até 31/12/2020).
    [Theory]
    [InlineData(21, "2021-01-01", 3)]
    [InlineData(22, "2021-01-01", 6)]
    [InlineData(27, "2021-01-01", 6)]
    [InlineData(28, "2021-01-01", 10)]
    [InlineData(30, "2021-01-01", 10)]
    [InlineData(31, "2021-01-01", 15)]
    [InlineData(41, "2021-01-01", 15)]
    [InlineData(42, "2021-01-01", 20)]
    [InlineData(44, "2026-06-01", 20)]
    [InlineData(45, "2026-06-01", null)]
    [InlineData(20, "2020-12-31", 3)]
    [InlineData(21, "2020-12-31", 6)]
    [InlineData(26, "2020-12-31", 6)]
    [InlineData(27, "2020-12-31", 10)]
    [InlineData(29, "2020-12-31", 10)]
    [InlineData(30, "2020-12-31", 15)]
    [InlineData(40, "2020-12-31", 15)]
    [InlineData(41, "2020-12-31", 20)]
    [InlineData(43, "2020-12-31", 20)]
    [InlineData(44, "2020-12-31", null)]
    public void Duracao_pela_idade_do_conjuge(int idade, string obito, int? anos)
    {
        Assert.Equal(anos, CalculadoraPensaoMorte.DuracaoPorIdade(idade, DateOnly.Parse(obito, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Obito_antes_da_reforma_e_recusado()
    {
        Assert.Contains("a partir de 13/11/2019", CalculadoraPensaoMorte.Calcular(Entrada(obito: new DateOnly(2019, 11, 12))).Erro.Mensagem);
        Assert.Contains("ao menos um dependente", CalculadoraPensaoMorte.Calcular(Entrada(conjuge: false, outros: 0)).Erro.Mensagem);
    }
}
