using System.Text.Json;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class HistoricoMobileTests
{
    [Fact]
    public void Formulario_com_grade_reabre_com_ordem_campos_e_opcoes()
    {
        var original = Criar();
        Assert.Single(original.Campos, campo => campo.Chave == "salarioAnterior").Valor = "3.500,00";
        var grade = Assert.Single(original.Grades, grade => grade.Chave == "meses");
        var primeiro = grade.AdicionarLinha();
        Assert.NotNull(primeiro);
        primeiro.Campos[0].Valor = "01/2026";
        var segundo = grade.AdicionarLinha();
        Assert.NotNull(segundo);
        segundo.Campos[0].Valor = "02/2026";
        var json = JsonSerializer.Serialize(original.ExportarDados());
        var destino = Criar();

        var resultado = destino.TentarImportarDados(JsonSerializer.Deserialize<DadosFormularioMobile>(json)!);

        Assert.True(resultado.Sucesso);
        Assert.Equal("3.500,00", Assert.Single(destino.Campos, campo => campo.Chave == "salarioAnterior").Valor);
        Assert.Equal(new[] { "01/2026", "02/2026" }, destino.Grades[0].Linhas.Select(linha => linha.Campos[0].Valor));
    }

    [Fact]
    public void Importacao_invalida_nao_substitui_o_formulario()
    {
        var destino = Criar();
        var antes = destino.ExportarDados();
        var invalido = antes with { Grades = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, ValorCampoMobile>>>
        {
            [destino.Grades[0].Chave] = [new Dictionary<string, ValorCampoMobile> { ["colunaDesconhecida"] = new("1", 0, false) }]
        } };

        var resultado = destino.TentarImportarDados(invalido);

        Assert.True(resultado.Falhou);
        Assert.Equal(JsonSerializer.Serialize(antes), JsonSerializer.Serialize(destino.ExportarDados()));
        Assert.Empty(destino.Grades[0].Linhas);
    }

    private static ReajusteRetroativoMobileViewModel Criar() => new(new SimulacaoFalsa(), new PdfFalso(),
        NullLogger<ReajusteRetroativoMobileViewModel>.Instance);

    private sealed class SimulacaoFalsa : ISimularDemonstrativoUseCase<SimularReajusteRetroativoRequest>
    {
        public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularReajusteRetroativoRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class PdfFalso : ICompartilhamentoPdf
    {
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
