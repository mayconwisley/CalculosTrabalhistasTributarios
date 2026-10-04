using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf.ComponentesPdf;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf;

/// <summary>Relatório da simulação tributária: INSS, IRRF nas duas modalidades e a memória de cálculo.</summary>
internal static class RelatorioPdfImposto
{
    internal static Task GerarRelatorioImpostoAsync(SimulacaoImpostoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) =>
        GerarAsync(caminhoArquivo, cancellationToken, documento => documento.Page(pagina =>
        {
            ConfigurarPagina(pagina, "Relatório de simulação tributária", simulacao.Entrada.Competencia);
            pagina.Content().Column(coluna =>
            {
                coluna.Spacing(16);
                coluna.Item().Element(container => CriarResumoImposto(container, simulacao));
                coluna.Item().Element(container => CriarComparativoIrrf(container, simulacao));
                // Antes de 05/2023 não havia desconto simplificado: só a modalidade normal aparece.
                var temSimplificado = simulacao.DescontoSimplificado is not null;
                coluna.Item().Element(container => CriarMemoriaIrrf(container, simulacao.Normal, FormulaBaseIrrf.Normal(simulacao, Moeda)));
                if (temSimplificado)
                    coluna.Item().Element(container => CriarMemoriaIrrf(container, simulacao.Simplificada, FormulaBaseIrrf.Simplificada(simulacao, Moeda)));
                coluna.Item().Element(container => CriarDetalhesFaixas(container, "Detalhamento do INSS", simulacao.DetalhesInss, $"Total do INSS: {Moeda(simulacao.ValorInss)}"));
                coluna.Item().Element(container => CriarDetalhesFaixas(container, "IRRF normal - cálculo progressivo", simulacao.Normal.DetalhesProgressivos, TotalProgressivo(simulacao.Normal)));
                if (temSimplificado)
                    coluna.Item().Element(container => CriarDetalhesFaixas(container, "IRRF simplificado - cálculo progressivo", simulacao.Simplificada.DetalhesProgressivos, TotalProgressivo(simulacao.Simplificada)));
            });
        }));

    private static void CriarResumoImposto(IContainer container, SimulacaoImpostoDto simulacao)
    {
        CriarSecao(container, "Dados considerados", secao => secao.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas => { colunas.RelativeColumn(); colunas.RelativeColumn(); colunas.RelativeColumn(); colunas.RelativeColumn(); });
            CelulaRotulo(tabela.Cell(), "Valor bruto"); CelulaValor(tabela.Cell(), Moeda(simulacao.Entrada.ValorBruto));
            CelulaRotulo(tabela.Cell(), "Base de INSS"); CelulaValor(tabela.Cell(), Moeda(simulacao.BaseInssConsiderada));
            CelulaRotulo(tabela.Cell(), "Dependentes"); CelulaValor(tabela.Cell(), simulacao.Entrada.QuantidadeDependentes.ToString(CulturaPtBr));
            CelulaRotulo(tabela.Cell(), "INSS calculado"); CelulaValor(tabela.Cell(), Moeda(simulacao.ValorInss));
            CelulaRotulo(tabela.Cell(), "FGTS padrão (8%)"); CelulaValor(tabela.Cell(), Moeda(simulacao.FgtsOitoPorCento));
            CelulaRotulo(tabela.Cell(), "FGTS Jovem Aprendiz (2%)"); CelulaValor(tabela.Cell(), Moeda(simulacao.FgtsDoisPorCento));
            CelulaRotulo(tabela.Cell(), "IRRF descontado"); CelulaValor(tabela.Cell(), simulacao.RetencaoDispensada ? $"{Moeda(0m)} (dispensado: {Moeda(simulacao.IrrfCalculado)} até {Moeda(simulacao.DescontoMinimo)})" : Moeda(simulacao.IrrfAplicado));
            CelulaRotulo(tabela.Cell(), "Salário líquido"); CelulaValor(tabela.Cell(), Moeda(simulacao.SalarioLiquido));
        }));
    }

    private static void CriarComparativoIrrf(IContainer container, SimulacaoImpostoDto simulacao)
    {
        CriarSecao(container, "Comparativo de IRRF", secao => secao.Column(coluna =>
        {
            coluna.Spacing(8);
            var modalidadeMaisVantajosa = simulacao.ModalidadeMaisVantajosa;
            if (modalidadeMaisVantajosa is not null)
                coluna.Item().Background(VerdeClaroVantagem).Border(1).BorderColor(VerdeBordaVantagem).Padding(8).Text(simulacao.MensagemVantagem).SemiBold().FontColor(VerdeVantagem);
            else
                coluna.Item().Text(simulacao.MensagemVantagem).SemiBold().FontColor(AzulPrimario);
            coluna.Item().Table(tabela =>
            {
                tabela.ColumnsDefinition(colunas => { colunas.RelativeColumn(2); colunas.RelativeColumn(); colunas.RelativeColumn(); colunas.RelativeColumn(); });
                CabecalhoTabela(tabela.Cell(), "Modalidade"); CabecalhoTabela(tabela.Cell(), "Base de cálculo"); CabecalhoTabela(tabela.Cell(), "Redução mensal"); CabecalhoTabela(tabela.Cell(), "IRRF final");
                LinhaIrrf(tabela, simulacao.Normal, EhMaisVantajosa(simulacao.Normal, modalidadeMaisVantajosa));
                if (simulacao.DescontoSimplificado is not null)
                    LinhaIrrf(tabela, simulacao.Simplificada, EhMaisVantajosa(simulacao.Simplificada, modalidadeMaisVantajosa));
            });
        }));
    }

    private static void LinhaIrrf(TableDescriptor tabela, ModalidadeIrrfDto modalidade, bool ehMaisVantajosa)
    {
        var nomeModalidade = ehMaisVantajosa ? $"{modalidade.Nome} - MAIS VANTAJOSO" : modalidade.Nome;
        CelulaTabela(tabela.Cell(), nomeModalidade, ehMaisVantajosa);
        CelulaTabela(tabela.Cell(), Moeda(modalidade.BaseCalculo), ehMaisVantajosa);
        CelulaTabela(tabela.Cell(), Moeda(modalidade.ReducaoMensal), ehMaisVantajosa);
        CelulaTabela(tabela.Cell(), Moeda(modalidade.Imposto), ehMaisVantajosa);
    }

    private static void CriarMemoriaIrrf(IContainer container, ModalidadeIrrfDto modalidade, string formulaBase)
    {
        CriarSecao(container, $"Memória de cálculo do IRRF - {modalidade.Nome}", secao => secao.Border(1).BorderColor(CinzaBorda).Column(cartao =>
        {
            cartao.Item().Background(AzulPrimario).Padding(6).Text("Cálculo consolidado").FontColor(Colors.White).SemiBold();
            cartao.Item().Padding(8).Column(memoria =>
            {
                memoria.Spacing(3);
                AdicionarFormulaIrrf(memoria, "Base de cálculo", formulaBase);
                AdicionarFormulaIrrf(memoria, "IR progressivo", $"{Moeda(modalidade.BaseCalculo)} x {Percentual(modalidade.Aliquota)} - {Moeda(modalidade.Deducao)} = {Moeda(modalidade.ImpostoAntesReducao)}");
                AdicionarFormulaIrrf(memoria, "IRRF após redução mensal", $"{Moeda(modalidade.ImpostoAntesReducao)} - {Moeda(modalidade.ReducaoMensal)} = {Moeda(modalidade.Imposto)}");
            });
        }), $"IRRF final: {Moeda(modalidade.Imposto)}");
    }

    // As faixas são arredondadas uma a uma; o total é o imposto pela parcela a deduzir, antes da redução mensal.
    private static string TotalProgressivo(ModalidadeIrrfDto modalidade) =>
        modalidade.ReducaoMensal > 0m ? $"Imposto antes da redução mensal: {Moeda(modalidade.ImpostoAntesReducao)}" : $"Imposto: {Moeda(modalidade.ImpostoAntesReducao)}";

    private static void CriarDetalhesFaixas(IContainer container, string titulo, IReadOnlyList<DetalheFaixaDto> detalhes, string total)
    {
        CriarSecao(container, titulo, secao => secao.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas => { colunas.ConstantColumn(52); colunas.RelativeColumn(); colunas.RelativeColumn(); colunas.RelativeColumn(); });
            CabecalhoTabela(tabela.Cell(), "Faixa"); CabecalhoTabela(tabela.Cell(), "Base calculada"); CabecalhoTabela(tabela.Cell(), "Alíquota"); CabecalhoTabela(tabela.Cell(), "Imposto");
            foreach (var detalhe in detalhes)
            {
                CelulaTabela(tabela.Cell(), detalhe.Faixa.ToString(CulturaPtBr));
                CelulaTabela(tabela.Cell(), Moeda(detalhe.BaseCalculada));
                CelulaTabela(tabela.Cell(), Percentual(detalhe.Aliquota));
                CelulaTabela(tabela.Cell(), Moeda(detalhe.Imposto));
            }
        }), total);
    }

    private static bool EhMaisVantajosa(ModalidadeIrrfDto modalidade, string? modalidadeMaisVantajosa) =>
        modalidade.Nome.Equals(modalidadeMaisVantajosa, StringComparison.OrdinalIgnoreCase);
}
