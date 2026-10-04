using CalculosTrabalhistasTributarios.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf.ComponentesPdf;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf;

/// <summary>Demonstrativo da indenização do período de estabilidade.</summary>
internal static class RelatorioPdfEstabilidade
{
    internal static Task GerarRelatorioEstabilidadeAsync(SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada, string caminhoArquivo, CancellationToken cancellationToken) =>
        GerarAsync(caminhoArquivo, cancellationToken, documento => documento.Page(pagina =>
        {
            ConfigurarPagina(pagina, "Demonstrativo de cálculo de estabilidade", $"Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}");
            pagina.Content().Column(coluna =>
            {
                coluna.Spacing(16);
                coluna.Item().Element(container => CriarVerbasEstabilidade(container, simulacao));
                coluna.Item().Element(container => CriarDadosEstabilidade(container, simulacao, entrada));
                coluna.Item().Background(AzulPrimario).Padding(14).Row(total =>
                {
                    total.RelativeItem().Text("Total a receber").FontSize(14).SemiBold().FontColor(Colors.White);
                    total.AutoItem().Text(Moeda(simulacao.Total)).FontSize(16).SemiBold().FontColor(Colors.White);
                });
            });
        }));

    private static void CriarVerbasEstabilidade(IContainer container, SimulacaoEstabilidadeDto simulacao)
    {
        CriarSecao(container, "Valores da indenização", secao => secao.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas => { colunas.RelativeColumn(2); colunas.RelativeColumn(); });
            CabecalhoTabela(tabela.Cell(), "Estabilidade"); CabecalhoTabela(tabela.Cell(), "Valor");
            LinhaEstabilidade(tabela, "Indenização", simulacao.Indenizacao);
            LinhaEstabilidade(tabela, "13º salário", simulacao.DecimoTerceiro);
            LinhaEstabilidade(tabela, "Férias", simulacao.Ferias);
            LinhaEstabilidade(tabela, "1/3 de férias", simulacao.TercoFerias);
            LinhaEstabilidade(tabela, "FGTS 8%", simulacao.FgtsOitoPorCento);
            LinhaEstabilidade(tabela, "FGTS 40%", simulacao.MultaFgtsQuarentaPorCento);
            LinhaEstabilidade(tabela, "Complementos", simulacao.Complementos);
            LinhaEstabilidade(tabela, "Subtotal", simulacao.Total, true);
        }));
    }

    private static void CriarDadosEstabilidade(IContainer container, SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada)
    {
        CriarSecao(container, "Dados considerados no cálculo", secao => secao.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.RelativeColumn(14); colunas.RelativeColumn(10);
                colunas.RelativeColumn(14); colunas.RelativeColumn(10);
            });
            CelulaRotulo(tabela.Cell(), "Demissão"); CelulaValor(tabela.Cell(), entrada.Demissao.ToString("dd/MM/yyyy", CulturaPtBr));
            CelulaRotulo(tabela.Cell(), "Fim da estabilidade"); CelulaValor(tabela.Cell(), entrada.FimEstabilidade.ToString("dd/MM/yyyy", CulturaPtBr));
            CelulaRotulo(tabela.Cell(), "Média para cálculo"); CelulaValor(tabela.Cell(), Moeda(entrada.MediaRemuneratoria));
            CelulaRotulo(tabela.Cell(), "Dias-base"); CelulaValor(tabela.Cell(), entrada.DiasBase.ToString(CulturaPtBr));
            CelulaRotulo(tabela.Cell(), "Dias de estabilidade"); CelulaValor(tabela.Cell(), simulacao.DiasEstabilidade.ToString(CulturaPtBr));
            CelulaRotulo(tabela.Cell(), "Meses e dias"); CelulaValor(tabela.Cell(), $"{simulacao.Meses} mês(es) e {simulacao.DiasAlemDosMeses} dia(s)");
            CelulaRotulo(tabela.Cell(), "Avos de 13º"); CelulaValor(tabela.Cell(), simulacao.AvosDecimoTerceiro.ToString(CulturaPtBr));
            CelulaRotulo(tabela.Cell(), "Avos de férias"); CelulaValor(tabela.Cell(), simulacao.AvosFerias.ToString(CulturaPtBr));
        }));
    }

    private static void LinhaEstabilidade(TableDescriptor tabela, string descricao, decimal valor, bool destaque = false)
    {
        var corFundo = destaque ? AzulClaro : null;
        var celulaDescricao = tabela.Cell().BorderBottom(1).BorderColor(CinzaBorda).Padding(6);
        var celulaValor = tabela.Cell().BorderBottom(1).BorderColor(CinzaBorda).Padding(6);
        if (corFundo is not null)
        {
            celulaDescricao = celulaDescricao.Background(corFundo);
            celulaValor = celulaValor.Background(corFundo);
        }

        var textoDescricao = celulaDescricao.Text(descricao).FontColor(CinzaTexto);
        var textoValor = celulaValor.AlignRight().Text(Moeda(valor)).FontColor(CinzaTexto);
        if (destaque)
        {
            textoDescricao.SemiBold();
            textoValor.SemiBold();
        }
    }
}
