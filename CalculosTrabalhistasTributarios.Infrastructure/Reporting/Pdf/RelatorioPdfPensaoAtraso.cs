using CalculosTrabalhistasTributarios.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf.ComponentesPdf;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf;

/// <summary>Relatório das parcelas de pensão em atraso, corrigidas e com juros.</summary>
internal static class RelatorioPdfPensaoAtraso
{
    internal static Task GerarRelatorioPensaoAtrasoAsync(SimulacaoPensaoAtrasoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) =>
        GerarAsync(caminhoArquivo, cancellationToken, documento => documento.Page(pagina =>
        {
            ConfigurarPagina(pagina, "Cálculo de pensão alimentícia em atraso", $"Cálculo em {simulacao.DataCalculo:dd/MM/yyyy}  |  Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}");
            pagina.Content().Column(coluna =>
            {
                coluna.Spacing(16);
                coluna.Item().Element(container => CriarSecao(container, "Resumo do débito", secao => secao.Table(tabela =>
                {
                    tabela.ColumnsDefinition(colunas => { colunas.RelativeColumn(3); colunas.RelativeColumn(2); colunas.RelativeColumn(3); colunas.RelativeColumn(2); });
                    CelulaRotulo(tabela.Cell(), "Valor devido"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalDevido));
                    CelulaRotulo(tabela.Cell(), "Pagamentos parciais"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalPago));
                    CelulaRotulo(tabela.Cell(), "Saldo original"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalSaldo));
                    CelulaRotulo(tabela.Cell(), $"Correção ({SimulacaoPensaoAtrasoDto.NomeCorrecao(simulacao.Correcao)})"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalCorrecao));
                    CelulaRotulo(tabela.Cell(), "Juros de mora"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalJuros));
                    CelulaRotulo(tabela.Cell(), "Total atualizado"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalAtualizado));
                    CelulaRotulo(tabela.Cell(), $"Rito da prisão ({simulacao.QuantidadePrisao} parcela(s))"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalPrisao));
                    CelulaRotulo(tabela.Cell(), $"Rito da penhora ({simulacao.QuantidadePenhora} parcela(s))"); CelulaValor(tabela.Cell(), Moeda(simulacao.TotalPenhora));
                    if (simulacao.TemAcrescimos)
                    {
                        CelulaRotulo(tabela.Cell(), "Multa de 10% (CPC, art. 523)"); CelulaValor(tabela.Cell(), Moeda(simulacao.Multa));
                        CelulaRotulo(tabela.Cell(), "Honorários de 10% (CPC, art. 523)"); CelulaValor(tabela.Cell(), simulacao.Honorarios > 0m ? Moeda(simulacao.Honorarios) : "Não incluídos");
                    }
                }), simulacao.TemAcrescimos ? $"Total com acréscimos: {Moeda(simulacao.TotalComAcrescimos)}" : $"Total: {Moeda(simulacao.TotalAtualizado)}"));
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
                        colunas.RelativeColumn(5); colunas.RelativeColumn(6); colunas.RelativeColumn(6); colunas.RelativeColumn(5);
                        colunas.RelativeColumn(6); colunas.RelativeColumn(5); colunas.RelativeColumn(5); colunas.RelativeColumn(6); colunas.RelativeColumn(5);
                    });
                    foreach (var titulo in new[] { "Mês", "Vencimento", "Saldo", "Fator", "Corrigido", "Juros %", "Juros", "Total", "Rito" })
                        tabela.Cell().Background(AzulPrimario).Padding(4).Text(titulo).FontSize(8).FontColor(Colors.White).SemiBold();
                    foreach (var parcela in simulacao.Parcelas)
                    {
                        foreach (var valor in new[]
                        {
                            parcela.Competencia.ToString("MM/yyyy", CulturaPtBr), parcela.Vencimento.ToString("dd/MM/yyyy", CulturaPtBr), Moeda(parcela.Saldo),
                            parcela.FatorCorrecao.ToString("N6", CulturaPtBr), Moeda(parcela.Corrigido), parcela.PercentualJuros.ToString("N4", CulturaPtBr) + "%",
                            Moeda(parcela.Juros), Moeda(parcela.Total), parcela.RitoPrisao ? "Prisão" : "Penhora"
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
