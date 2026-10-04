using CalculosTrabalhistasTributarios.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf.ComponentesPdf;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf;

/// <summary>Relatório da atualização de débitos judiciais trabalhistas e cíveis.</summary>
internal static class RelatorioPdfDebitoJudicial
{
    internal static Task GerarRelatorioDebitoJudicialAsync(SimulacaoDebitoJudicialDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) =>
        GerarAsync(caminhoArquivo, cancellationToken, documento => documento.Page(pagina =>
        {
            ConfigurarPagina(pagina, $"Atualização de {simulacao.NomeNatureza.ToLowerInvariant()}", $"Cálculo em {simulacao.DataCalculo:dd/MM/yyyy}  |  Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}");
            pagina.Content().Column(coluna =>
            {
                coluna.Spacing(16);
                coluna.Item().Element(container => CriarSecao(container, "Resumo do débito", secao => secao.Table(tabela =>
                {
                    tabela.ColumnsDefinition(colunas => { colunas.RelativeColumn(3); colunas.RelativeColumn(2); colunas.RelativeColumn(3); colunas.RelativeColumn(2); });
                    CelulaRotulo(tabela.Cell(), "Valor original"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalValor));
                    CelulaRotulo(tabela.Cell(), "Correção"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalCorrecao));
                    CelulaRotulo(tabela.Cell(), "Valor atualizado"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalAtualizado));
                    CelulaRotulo(tabela.Cell(), "Juros"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalJuros));
                    if (simulacao.TemAcrescimos)
                    {
                        CelulaRotulo(tabela.Cell(), "Multa de 10% (CPC, art. 523)"); CelulaValor(tabela.Cell(), Moeda(simulacao.Multa));
                        CelulaRotulo(tabela.Cell(), "Honorários de 10% (CPC, art. 523)"); CelulaValor(tabela.Cell(), simulacao.Honorarios > 0m ? Moeda(simulacao.Honorarios) : "Não incluídos");
                    }
                }), simulacao.TemAcrescimos ? $"Total com acréscimos: {Moeda(simulacao.TotalComAcrescimos)}" : $"Total: {Moeda(simulacao.Total)}"));
                coluna.Item().Element(container => CriarSecao(container, "Critérios", secao => secao.Column(criterios =>
                {
                    criterios.Spacing(4);
                    foreach (var criterio in simulacao.Criterios)
                        criterios.Item().Text($"• {criterio}");
                })));
                coluna.Item().Element(container => CriarSecao(container, "Parcelas", secao => secao.Table(tabela =>
                {
                    tabela.ColumnsDefinition(colunas =>
                    {
                        colunas.RelativeColumn(7); colunas.RelativeColumn(5); colunas.RelativeColumn(5); colunas.RelativeColumn(5);
                        colunas.RelativeColumn(4); colunas.RelativeColumn(5); colunas.RelativeColumn(4); colunas.RelativeColumn(5); colunas.RelativeColumn(5);
                    });
                    foreach (var titulo in new[] { "Descrição", "Vencimento", "Valor", "Fator", "Selic", "Atualizado", "Juros %", "Juros", "Total" })
                        tabela.Cell().Background(AzulPrimario).Padding(4).Text(titulo).FontSize(8).FontColor(Colors.White).SemiBold();
                    foreach (var parcela in simulacao.Parcelas)
                    {
                        foreach (var valor in new[]
                        {
                            parcela.Descricao, parcela.Vencimento.ToString("dd/MM/yyyy", CulturaPtBr), Moeda(parcela.Valor), parcela.FatorCorrecao.ToString("N6", CulturaPtBr),
                            parcela.PercentualSelic > 0m ? parcela.PercentualSelic.ToString("N4", CulturaPtBr) + "%" : "", Moeda(parcela.Atualizado),
                            parcela.PercentualJuros.ToString("N4", CulturaPtBr) + "%", Moeda(parcela.Juros), Moeda(parcela.Total)
                        })
                            tabela.Cell().BorderBottom(1).BorderColor(CinzaBorda).Padding(4).Text(valor).FontSize(8);
                    }
                })));
                if (simulacao.Observacoes.Count > 0)
                    coluna.Item().Element(container => CriarSecao(container, "Observações", secao => secao.Column(observacoes =>
                    {
                        observacoes.Spacing(4);
                        foreach (var observacao in simulacao.Observacoes)
                            observacoes.Item().Text($"• {observacao}");
                    })));
            });
        }));
}
