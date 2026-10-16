using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Dias sem expediente bancário (Resolução CMN 4.880/2020, art. 6º) e os vencimentos que dependem deles. As Páscoas são as
/// das tabelas publicadas do calendário gregoriano; os dias da semana foram conferidos à parte.
/// </summary>
public class CalendarioBancarioTests
{
    [Theory]
    [InlineData(2000, 4, 23)]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2028, 4, 16)]
    [InlineData(2038, 4, 25)]
    public void Pascoa(int ano, int mes, int dia) => Assert.Equal(new DateOnly(ano, mes, dia), CalendarioBancario.Pascoa(ano));

    [Theory]
    [InlineData("2026-01-01")]
    [InlineData("2026-02-16")]   // segunda-feira de Carnaval: Páscoa em 05/04/2026 menos 48 dias
    [InlineData("2026-02-17")]   // terça-feira de Carnaval
    [InlineData("2026-04-03")]   // Sexta-feira da Paixão
    [InlineData("2026-04-21")]
    [InlineData("2026-05-01")]
    [InlineData("2026-06-04")]   // Corpus Christi: Páscoa mais 60 dias
    [InlineData("2026-09-07")]
    [InlineData("2026-10-12")]
    [InlineData("2026-11-02")]
    [InlineData("2026-11-20")]
    [InlineData("2026-12-25")]
    public void Feriados_sem_expediente_bancario(string data) => Assert.False(CalendarioBancario.DiaUtil(Dia(data)));

    [Theory]
    [InlineData("2026-02-18")]   // Quarta-feira de Cinzas: expediente a partir do meio-dia, mas é dia útil
    [InlineData("2023-11-20")]   // 20/11 só é feriado nacional desde 2024 (Lei 14.759/2023)
    [InlineData("2026-12-24")]
    public void Dias_uteis(string data) => Assert.True(CalendarioBancario.DiaUtil(Dia(data)));

    [Fact]
    public void Anterior_e_seguinte_pulam_fim_de_semana_e_feriados()
    {
        // Carnaval de 2028 em 28 e 29/02, depois do fim de semana: o último dia útil de fevereiro é a sexta, 25/02.
        Assert.Equal(Dia("2028-02-25"), CalendarioBancario.DiaUtilAnterior(Dia("2028-02-29")));
        // Sábado 14/11/2026, domingo 15/11 (feriado) e segunda 16/11: o próximo dia útil é a segunda.
        Assert.Equal(Dia("2026-11-16"), CalendarioBancario.ProximoDiaUtil(Dia("2026-11-14")));
        Assert.Equal(Dia("2026-04-15"), CalendarioBancario.ProximoDiaUtil(Dia("2026-04-15")));
    }

    [Fact]
    public void Gps_do_autonomo_passa_para_o_dia_util_seguinte_ao_feriado()
    {
        // Competência 10/2027: o dia 15/11/2027 é uma segunda-feira de feriado; vence na terça, 16/11 (Lei 8.212/1991, art. 30, § 2º, I).
        Assert.Equal(Dia("2027-11-16"), CalculadoraContribuicaoSegurado.Vencimento(new DateOnly(2027, 10, 1)));
        Assert.Equal(Dia("2026-04-15"), CalculadoraContribuicaoSegurado.Vencimento(new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public void Fgts_e_antecipado_para_o_dia_util_anterior_ao_feriado()
    {
        // Competência 10/2025: o dia 20/11/2025 é uma quinta-feira de feriado; o depósito vence na quarta, 19/11.
        var apuracao = ConferenciaFgts.Calcular(CategoriaFgts.Empregado, [new LancamentoFgts(new DateOnly(2025, 10, 1), TipoCompetenciaFgts.Mensal, 3000m, 240m)], null).Sucesso();
        Assert.Equal(Dia("2025-11-19"), apuracao.Linhas.Single().Vencimento);
    }

    private static DateOnly Dia(string data) => DateOnly.Parse(data, System.Globalization.CultureInfo.InvariantCulture);
}
