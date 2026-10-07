using CalculosTrabalhistasTributarios.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf.ComponentesPdf;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf;

/// <summary>Demonstrativo das calculadoras no formato de holerite: proventos, descontos, informativos e memória.</summary>
internal static class RelatorioPdfDemonstrativo
{
    internal static Task GerarDemonstrativoAsync(DemonstrativoDto demonstrativo, string caminhoArquivo, CancellationToken cancellationToken) =>
        GerarAsync(caminhoArquivo, cancellationToken, documento => documento.Page(pagina =>
        {
            ConfigurarPagina(pagina, demonstrativo.Titulo, $"{demonstrativo.Referencia}  |  Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}");
            pagina.Content().Column(coluna =>
            {
                coluna.Spacing(16);
                coluna.Item().Element(container => CriarResumoDemonstrativo(container, demonstrativo));
                if (demonstrativo.Comparativo is { } comparativo)
                    coluna.Item().Element(container => CriarComparativoDemonstrativo(container, comparativo));
                if (demonstrativo.TemVerbas)
                    coluna.Item().Element(container => CriarVerbasDemonstrativo(container, demonstrativo));
                if (demonstrativo.Informativos.Count > 0)
                    coluna.Item().Element(container => CriarInformativosDemonstrativo(container, demonstrativo));
                foreach (var grupo in demonstrativo.Memoria)
                    coluna.Item().Element(container => CriarGrupoMemoria(container, grupo));
                if (demonstrativo.Observacoes.Count > 0)
                    coluna.Item().Element(container => CriarSecao(container, "Observações", secao => secao.Column(observacoes =>
                    {
                        observacoes.Spacing(4);
                        foreach (var observacao in demonstrativo.Observacoes)
                            observacoes.Item().Text($"• {observacao}");
                    })));
            });
        }));

    private static void CriarResumoDemonstrativo(IContainer container, DemonstrativoDto demonstrativo)
    {
        CriarSecao(container, "Resumo", secao => secao.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas => { colunas.RelativeColumn(2); colunas.RelativeColumn(2); colunas.RelativeColumn(3); });
            foreach (var destaque in demonstrativo.Destaques)
            {
                CelulaRotulo(tabela.Cell(), destaque.Rotulo);
                tabela.Cell().BorderBottom(1).BorderColor(CinzaBorda).Padding(6).Text(destaque.Valor).SemiBold();
                tabela.Cell().BorderBottom(1).BorderColor(CinzaBorda).Padding(6).Text(destaque.Complemento).FontSize(8);
            }
        }));
    }

    private static void CriarVerbasDemonstrativo(IContainer container, DemonstrativoDto demonstrativo)
    {
        CriarSecao(container, "Demonstrativo", secao => secao.Column(coluna =>
        {
            var temDescontos = demonstrativo.Descontos.Count > 0;
            coluna.Item().Table(tabela =>
            {
                tabela.ColumnsDefinition(colunas =>
                {
                    colunas.RelativeColumn(4); colunas.RelativeColumn(1.4f); colunas.RelativeColumn(1.6f);
                    if (temDescontos) colunas.RelativeColumn(1.6f);
                });
                CabecalhoTabela(tabela.Cell(), "Descrição"); CabecalhoTabela(tabela.Cell(), "Referência");
                tabela.Cell().Background(AzulPrimario).Padding(6).AlignRight().Text(demonstrativo.RotuloProventos).FontColor(Colors.White).SemiBold();
                if (temDescontos) tabela.Cell().Background(AzulPrimario).Padding(6).AlignRight().Text("Descontos").FontColor(Colors.White).SemiBold();

                foreach (var verba in demonstrativo.Proventos)
                    LinhaVerba(tabela, verba, Moeda(verba.Valor), string.Empty, temDescontos);
                foreach (var verba in demonstrativo.Descontos)
                    LinhaVerba(tabela, verba, string.Empty, Moeda(verba.Valor), temDescontos);

                tabela.Cell().ColumnSpan(2).Background(AzulClaro).Padding(6).Text("Totais").SemiBold();
                tabela.Cell().Background(AzulClaro).Padding(6).AlignRight().Text(Moeda(demonstrativo.TotalProventos)).SemiBold();
                if (temDescontos) tabela.Cell().Background(AzulClaro).Padding(6).AlignRight().Text(Moeda(demonstrativo.TotalDescontos)).SemiBold();
            });
            coluna.Item().Background(AzulPrimario).Padding(12).Row(total =>
            {
                total.RelativeItem().Text(demonstrativo.RotuloResultado).FontSize(13).SemiBold().FontColor(Colors.White);
                total.AutoItem().Text(Moeda(demonstrativo.Resultado)).FontSize(15).SemiBold().FontColor(Colors.White);
            });
        }));
    }

    private static void LinhaVerba(TableDescriptor tabela, VerbaDto verba, string provento, string desconto, bool temDescontos)
    {
        CelulaTabela(tabela.Cell(), verba.Descricao);
        CelulaTabela(tabela.Cell(), verba.Referencia);
        tabela.Cell().BorderBottom(1).BorderColor(CinzaBorda).Padding(6).AlignRight().Text(provento);
        if (temDescontos) tabela.Cell().BorderBottom(1).BorderColor(CinzaBorda).Padding(6).AlignRight().Text(desconto);
    }

    private static void CriarComparativoDemonstrativo(IContainer container, TabelaComparativaDto comparativo)
    {
        CriarSecao(container, comparativo.Titulo, secao => secao.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.RelativeColumn(3);
                foreach (var _ in comparativo.Colunas)
                    colunas.RelativeColumn(2);
            });
            CabecalhoTabela(tabela.Cell(), "");
            foreach (var titulo in comparativo.Colunas)
                CabecalhoTabela(tabela.Cell(), titulo);
            foreach (var linha in comparativo.Linhas)
            {
                var descricao = tabela.Cell().BorderBottom(1).BorderColor(CinzaBorda).Padding(6).Text(linha.Descricao);
                if (linha.Destaque) descricao.SemiBold();
                foreach (var valor in linha.Valores)
                {
                    var celula = tabela.Cell().BorderBottom(1).BorderColor(CinzaBorda).Padding(6).AlignRight().Text(valor);
                    if (linha.Destaque) celula.SemiBold();
                }
            }
        }));
    }

    private static void CriarInformativosDemonstrativo(IContainer container, DemonstrativoDto demonstrativo)
    {
        CriarSecao(container, "Valores informativos", secao => secao.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas => { colunas.RelativeColumn(4); colunas.RelativeColumn(1.4f); colunas.RelativeColumn(1.6f); });
            CabecalhoTabela(tabela.Cell(), "Descrição"); CabecalhoTabela(tabela.Cell(), "Referência");
            tabela.Cell().Background(AzulPrimario).Padding(6).AlignRight().Text("Valor").FontColor(Colors.White).SemiBold();
            foreach (var verba in demonstrativo.Informativos)
            {
                CelulaTabela(tabela.Cell(), verba.Descricao);
                CelulaTabela(tabela.Cell(), verba.Referencia);
                tabela.Cell().BorderBottom(1).BorderColor(CinzaBorda).Padding(6).AlignRight().Text(Moeda(verba.Valor));
            }
        }), "Não entram nos totais nem no resultado");
    }

    private static void CriarGrupoMemoria(IContainer container, GrupoMemoriaDto grupo)
    {
        // Reserva espaço para o título não ficar sozinho no fim da página, separado das fórmulas.
        CriarSecao(container.EnsureSpace(140), $"Memória de cálculo - {grupo.Titulo}", secao => secao.Border(1).BorderColor(CinzaBorda).Padding(8).Column(memoria =>
        {
            memoria.Spacing(3);
            foreach (var formula in grupo.Formulas)
                AdicionarFormulaIrrf(memoria, formula.Titulo, formula.Formula);
        }), grupo.Destaque);
    }
}
