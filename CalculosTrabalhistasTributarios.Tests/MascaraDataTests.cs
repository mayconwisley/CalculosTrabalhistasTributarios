using CalculosTrabalhistasTributarios.Presentation.Behaviors;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Máscara de digitação de datas e competências: barras inseridas, excesso descartado e colagem convertida.</summary>
public class MascaraDataTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("0", "0")]
    [InlineData("01", "01")]
    [InlineData("010", "01/0")]
    [InlineData("0105", "01/05")]
    [InlineData("01052", "01/05/2")]
    [InlineData("01052026", "01/05/2026")]
    [InlineData("010520269", "01/05/2026")]
    [InlineData("01/05/2026", "01/05/2026")]
    [InlineData("0a1/0b5", "01/05")]
    public void Data_recebe_as_barras_enquanto_digita(string digitado, string esperado) =>
        Assert.Equal(esperado, MascaraData.Formatar(digitado, MascaraData.Data));

    [Theory]
    [InlineData("1", "1")]
    [InlineData("10", "10")]
    [InlineData("102", "10/2")]
    [InlineData("102026", "10/2026")]
    [InlineData("1020261", "10/2026")]
    [InlineData("10/2026", "10/2026")]
    public void Competencia_recebe_a_barra_enquanto_digita(string digitado, string esperado) =>
        Assert.Equal(esperado, MascaraData.Formatar(digitado, MascaraData.Competencia));

    [Theory]
    [InlineData("1/5/2026", MascaraData.Data, "01/05/2026")]
    [InlineData(" 2026-05-01 ", MascaraData.Data, "01/05/2026")]
    [InlineData("01.05.2026", MascaraData.Data, "01/05/2026")]
    [InlineData("5/2026", MascaraData.Competencia, "05/2026")]
    [InlineData("2026-05", MascaraData.Competencia, "05/2026")]
    public void Colagem_em_outro_formato_e_convertida(string colado, string tipo, string esperado) =>
        Assert.Equal(esperado, MascaraData.Normalizar(colado, tipo));

    [Theory]
    [InlineData("31/02/2026", MascaraData.Data)]
    [InlineData("13/2026", MascaraData.Competencia)]
    [InlineData("texto", MascaraData.Data)]
    public void Colagem_invalida_nao_e_convertida(string colado, string tipo) =>
        Assert.Null(MascaraData.Normalizar(colado, tipo));

    [Fact]
    public void Campos_comuns_de_data_e_competencia_usam_a_mascara()
    {
        Assert.Equal(MascaraData.Data, new CampoTextoViewModel("Data", TipoCampo.Data, "").Mascara);
        Assert.Equal(MascaraData.Competencia, new CampoTextoViewModel("Competência", TipoCampo.Competencia, "").Mascara);
        Assert.Null(new CampoTextoViewModel("Salário", TipoCampo.Moeda, "0,00").Mascara);
        Assert.Null(new CampoTextoViewModel("Horas", TipoCampo.Horas, "0:00").Mascara);
    }
}
