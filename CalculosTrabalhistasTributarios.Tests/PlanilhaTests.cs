using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Infrastructure.Reporting;
using ClosedXML.Excel;
using System.IO;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class PlanilhaTests
{
    [Fact]
    public async Task Iof_automatico_exporta_valor_apurado_e_memoria()
    {
        var entrada = new EntradaCreditoTrabalhador(ObjetivoCreditoTrabalhador.ValorDesejado,
            1000m, 1, 0m, 0m, 0m, 0m, 0m, 0m, IofAutomatico: true,
            DataLiberacao: new(2026, 1, 1), PrimeiroVencimento: new(2026, 2, 1));
        var dto = await new SimularCreditoTrabalhadorUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new(new DateOnly(2026, 1, 1), 5000m, 0m, 0, 0m, 0m, entrada), default).Sucesso();
        using var pasta = await GerarAsync(caminho => new ClosedXmlPlanilhaService().GerarDemonstrativoAsync(dto, caminho, default));
        Assert.Equal(6.38d, Linha(pasta.Worksheet(1), "IOF financiado").Cell(3).GetDouble());
        Assert.Equal(1006.38d, Linha(pasta.Worksheet(1), "Principal financiado").Cell(3).GetDouble());
        Assert.Contains(pasta.Worksheet(2).CellsUsed(), c => c.GetString().Contains("31/01") || c.GetString().Contains("01/02/2026"));
        Assert.Equal(1000m, dto.Resultado);
    }

    private static async Task<XLWorkbook> GerarAsync(Func<string, Task> gerar)
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"planilha-teste-{Guid.NewGuid():N}.xlsx");
        try
        {
            await gerar(caminho);
            return new XLWorkbook(new MemoryStream(await File.ReadAllBytesAsync(caminho)));
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    private static IXLRow Linha(IXLWorksheet aba, string rotulo) => aba.RowsUsed().First(linha => linha.Cell(1).GetString() == rotulo);

    [Fact]
    public async Task Credito_trabalhador_exporta_credito_e_custos_numericos()
    {
        var entrada = new EntradaCreditoTrabalhador(ObjetivoCreditoTrabalhador.ValorDesejado, 1000m, 1, 10m, 50m, 50m, 100m, 0m, 0m);
        var dto = await new SimularCreditoTrabalhadorUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new(new DateOnly(2026, 10, 1), 4000m, 0m, 0, 0m, 0m, entrada), default).Sucesso();
        using var pasta = await GerarAsync(caminho => new ClosedXmlPlanilhaService().GerarDemonstrativoAsync(dto, caminho, default));
        var aba = pasta.Worksheet(1);
        Assert.Equal(1000d, Linha(aba, "Crédito antes dos custos da liberação").Cell(3).GetDouble());
        Assert.Equal(100d, Linha(aba, "Custos descontados na liberação").Cell(4).GetDouble());
        Assert.Equal(900d, aba.RowsUsed().Last(linha => linha.Cell(1).GetString() == "Crédito líquido na liberação").Cell(3).GetDouble());
        Assert.Equal(310d, Linha(aba, "Custo total sobre crédito líquido").Cell(3).GetDouble());
    }

    [Fact]
    public async Task Trabalho_exterior_exporta_valores_numericos_e_mesma_apuracao()
    {
        var pedido = new SimularTrabalhoExteriorRequest(new DateOnly(2026, 10, 15), "Estados Unidos", "USD",
            new EntradaTrabalhoExterior(VinculoTrabalhoExterior.EmpregoAssalariado, 10_000m, 50m, 2_000m,
                10m, 5m, 20m, 1m, 5m, 4.8m), true, 0m, 0, 0m, 0m);
        var demonstrativo = await new SimularTrabalhoExteriorUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(pedido, default).Sucesso();
        using var pasta = await GerarAsync(caminho => new ClosedXmlPlanilhaService().GerarDemonstrativoAsync(demonstrativo, caminho, default));
        var aba = pasta.Worksheet(1);
        Assert.Equal(48_000d, Linha(aba, "Remuneração convertida").Cell(3).GetDouble());
        Assert.Equal(24d, Linha(aba, "Juros cobrados pela instituição").Cell(4).GetDouble());
        Assert.Equal(35_873.71d, aba.RowsUsed().Last(linha => linha.Cell(1).GetString() == "Após câmbio e tributos").Cell(3).GetDouble(), 2);
        Assert.Equal("Memória de cálculo", pasta.Worksheet(2).Name);
    }

    [Fact]
    public async Task Saque_aniversario_exporta_bruto_repasse_e_disponivel_numericos()
    {
        var demonstrativo = await new SimularSaqueAniversarioUseCase()
            .ExecutarAsync(new(8000m, 11, 1800m), default).Sucesso();
        using var pasta = await GerarAsync(caminho => new ClosedXmlPlanilhaService().GerarDemonstrativoAsync(demonstrativo, caminho, default));
        var aba = pasta.Worksheet(1);
        Assert.Equal(2250d, Linha(aba, "Saque-aniversário").Cell(3).GetDouble());
        Assert.Equal(1800d, Linha(aba, "Antecipação de empréstimos anteriores").Cell(4).GetDouble());
        Assert.Equal(450d, aba.RowsUsed().Last(linha => linha.Cell(1).GetString() == "Disponível estimado").Cell(3).GetDouble());
    }

    [Fact]
    public async Task Antecipacao_exporta_impedimento_sem_tratar_saldo_livre_como_credito()
    {
        var analise = new EntradaAnaliseAntecipacaoFgts(new DateOnly(2026, 10, 7), ConfirmacaoAntecipacaoFgts.Sim,
            ConfirmacaoAntecipacaoFgts.Sim, ConfirmacaoAntecipacaoFgts.Sim);
        var dto = await new SimularSaqueAniversarioUseCase().ExecutarAsync(new(18000m, 4, 0m, 10000m, 0m, true, analise), default).Sucesso();
        using var pasta = await GerarAsync(caminho => new ClosedXmlPlanilhaService().GerarDemonstrativoAsync(dto, caminho, default));
        Assert.Contains(pasta.Worksheet(1).CellsUsed(), celula => celula.GetString() == "Impedimento informado");
        Assert.Equal(8000d, Linha(pasta.Worksheet(1), "Saldo fora da garantia informada").Cell(3).GetDouble());
        Assert.Contains(pasta.Worksheet(2).CellsUsed(), celula => celula.GetString().Contains("quitação da antecipação vigente"));
    }

    [Fact]
    public async Task Demonstrativo_tem_os_valores_como_numeros_e_a_memoria_em_outra_aba()
    {
        var demonstrativo = await new SimularHoleriteUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularHoleriteRequest(
            new DateOnly(2026, 10, 1), 3000m, GrauInsalubridade.Nenhum, false, 0m, 220m, 10m, 50m, 0m, 100m, 0m, 20m, 1, 0, 0, 0m, 0, 0, null, 0m, 0m, 0m), default).Sucesso();
        using var pasta = await GerarAsync(caminho => new ClosedXmlPlanilhaService().GerarDemonstrativoAsync(demonstrativo, caminho, default));

        var aba = pasta.Worksheet(1);
        Assert.Equal("Holerite do mês", aba.Name);
        var salario = Linha(aba, "Salário").Cell(3);
        Assert.Equal(3000d, salario.GetDouble());
        Assert.Contains("R$", salario.Style.NumberFormat.Format);
        // "Líquido a receber" aparece no resumo, como texto, e no fim do demonstrativo, como número.
        var liquido = aba.RowsUsed().Last(linha => linha.Cell(1).GetString() == "Líquido a receber").Cell(3);
        Assert.Equal((double)demonstrativo.Resultado, liquido.GetDouble(), 2);
        Assert.Equal("Memória de cálculo", pasta.Worksheet(2).Name);
    }

    [Fact]
    public async Task Parcelas_em_atraso_com_datas_fator_e_juros_numericos()
    {
        var simulador = new SimularPensaoAtrasoUseCase(await Ambiente.ConsultaAsync(), await Ambiente.IndicesAsync());
        var parcelas = await simulador.GerarParcelasAsync(new GerarParcelasAtrasoRequest(BasePensao.ValorFixo, 1000m, 0m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), 10), default).Sucesso();
        var resultado = await simulador.CalcularAsync(new SimularPensaoAtrasoRequest(parcelas, new DateOnly(2026, 10, 3), null, CorrecaoMonetaria.Inpc, JurosDeMora.TaxaLegal, AcrescimosPenhora.MultaEHonorarios), default).Sucesso();
        using var pasta = await GerarAsync(caminho => new ClosedXmlPlanilhaService().GerarPensaoAtrasoAsync(resultado, caminho, default));

        var aba = pasta.Worksheet(1);
        var primeira = Linha(aba, "01/2026");
        Assert.Equal(new DateTime(2026, 1, 10), primeira.Cell(2).GetDateTime());
        Assert.Equal((double)resultado.Parcelas[0].FatorCorrecao, primeira.Cell(6).GetDouble(), 6);
        Assert.Equal((double)(resultado.Parcelas[0].PercentualJuros / 100m), primeira.Cell(8).GetDouble(), 8);
        Assert.Equal((double)resultado.Multa, Linha(aba, "Multa de 10% (CPC, art. 523)").Cell(2).GetDouble(), 2);
        Assert.Equal((double)resultado.TotalComAcrescimos, Linha(aba, "Total com multa e honorários").Cell(2).GetDouble(), 2);
    }
}
