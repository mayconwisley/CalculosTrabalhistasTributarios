using CalculosTrabalhistasTributarios.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf.ComponentesPdf;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Pdf;

/// <summary>Relatório da pensão alimentícia com o comparativo das modalidades do IRRF.</summary>
internal static class RelatorioPdfPensao
{
    internal static Task GerarRelatorioPensaoAsync(SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada, bool incluirDetalhes, string caminhoArquivo, CancellationToken cancellationToken) =>
        GerarAsync(caminhoArquivo, cancellationToken, documento => documento.Page(pagina =>
        {
            ConfigurarPagina(pagina, "Relatório de cálculo de pensão alimentícia", entrada.Competencia);
            pagina.Content().Column(coluna =>
            {
                coluna.Spacing(16);
                coluna.Item().Element(container => CriarResumoPensao(container, simulacao, entrada));
                coluna.Item().Element(container => CriarComparativoPensao(container, simulacao, entrada));

                if (incluirDetalhes)
                {
                    coluna.Item().Element(container => CriarDetalhesPensao(container, simulacao.Normal, entrada, simulacao.ValorInss));
                    if (simulacao.SimplificadoDisponivel)
                    {
                        coluna.Item().PageBreak();
                        coluna.Item().Element(container => CriarDetalhesPensao(container, simulacao.Simplificada, entrada, simulacao.ValorInss));
                    }
                }
            });
        }));

    private static void CriarResumoPensao(IContainer container, SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada)
    {
        CriarSecao(container, "Dados considerados", secao => secao.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.RelativeColumn(14); colunas.RelativeColumn(8);
                colunas.RelativeColumn(14); colunas.RelativeColumn(8);
                colunas.RelativeColumn(14); colunas.RelativeColumn(8);
            });
            CelulaRotulo(tabela.Cell(), "Rendimentos"); CelulaValor(tabela.Cell(), Moeda(entrada.ValorBruto));
            CelulaRotulo(tabela.Cell(), "Base de INSS"); CelulaValor(tabela.Cell(), Moeda(entrada.BaseInss));
            CelulaRotulo(tabela.Cell(), "INSS calculado"); CelulaValor(tabela.Cell(), Moeda(simulacao.ValorInss));
            CelulaRotulo(tabela.Cell(), "Pensão"); CelulaValor(tabela.Cell(), DescreverPensoes(entrada));
            CelulaRotulo(tabela.Cell(), "Outros descontos"); CelulaValor(tabela.Cell(), Moeda(entrada.OutrosDescontos));
            CelulaRotulo(tabela.Cell(), "Dependentes"); CelulaValor(tabela.Cell(), entrada.Dependentes.ToString(CulturaPtBr));
        }));
    }

    private static void CriarComparativoPensao(IContainer container, SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada)
    {
        CriarSecao(container, "Comparativo dos modelos", secao => secao.Column(coluna =>
        {
            coluna.Spacing(8);
            coluna.Item().Text(MemoriaCalculoPensao.Explicacao(simulacao, entrada, Moeda, Percentual)).FontSize(9);
            coluna.Item().Text(MemoriaCalculoPensao.EfeitoNoIrrf(simulacao, Moeda)).FontSize(9);
            coluna.Item().Text(MemoriaCalculoPensao.Faixas(simulacao, Moeda)).FontSize(9);
            coluna.Item().Text(simulacao.MensagemVantagem).SemiBold().FontColor(AzulPrimario);
            coluna.Item().Table(tabela =>
            {
                tabela.ColumnsDefinition(colunas => { colunas.RelativeColumn(2); colunas.RelativeColumn(2); colunas.RelativeColumn(); colunas.RelativeColumn(); colunas.RelativeColumn(); });
                CabecalhoTabela(tabela.Cell(), "Modelo"); CabecalhoTabela(tabela.Cell(), "Faixa do IRRF"); CabecalhoTabela(tabela.Cell(), "IRRF final"); CabecalhoTabela(tabela.Cell(), "Pensão"); CabecalhoTabela(tabela.Cell(), "IRRF + pensão");
                foreach (var modalidade in simulacao.Modalidades)
                    LinhaPensao(tabela, modalidade);
            });
            // Com mais de um beneficiário, a pensão de cada um na modalidade aplicada.
            var pensoes = simulacao.Aplicada.Detalhes[^1].Beneficiarios;
            if (pensoes.Count > 1)
                coluna.Item().Table(tabela =>
                {
                    tabela.ColumnsDefinition(colunas => { colunas.RelativeColumn(2); colunas.RelativeColumn(3); colunas.RelativeColumn(); });
                    CabecalhoTabela(tabela.Cell(), "Beneficiário"); CabecalhoTabela(tabela.Cell(), "Regra"); CabecalhoTabela(tabela.Cell(), "Pensão");
                    foreach (var pensao in pensoes)
                    {
                        CelulaTabela(tabela.Cell(), pensao.Nome);
                        CelulaTabela(tabela.Cell(), pensao.Descrever(Moeda, Percentual));
                        CelulaTabela(tabela.Cell(), Moeda(pensao.Pensao));
                    }
                });
        }));
    }

    private static string DescreverPensoes(EntradaPensaoDto entrada) => entrada.Beneficiarios.Count == 1
        ? entrada.Beneficiarios[0].Regra.Descrever(Moeda, Percentual)
        : $"{entrada.Beneficiarios.Count} beneficiários{(entrada.Sucessiva ? ", cada um após descontar os anteriores" : ", sobre a mesma base")}";

    private static void LinhaPensao(TableDescriptor tabela, ModalidadePensaoDto modalidade)
    {
        CelulaTabela(tabela.Cell(), modalidade.Nome);
        CelulaTabela(tabela.Cell(), MemoriaCalculoPensao.FaixaIrrf(modalidade.Detalhes[^1].Aliquota, modalidade.Imposto));
        CelulaTabela(tabela.Cell(), Moeda(modalidade.Imposto));
        CelulaTabela(tabela.Cell(), Moeda(modalidade.Pensao));
        CelulaTabela(tabela.Cell(), Moeda(modalidade.Total));
    }

    private static void CriarDetalhesPensao(IContainer container, ModalidadePensaoDto modalidade, EntradaPensaoDto entrada, decimal valorInss)
    {
        var rendimentosTributaveis = entrada.ValorBruto - entrada.OutrosDescontos;

        CriarSecao(container, $"{modalidade.Nome} - iterações do cálculo", secao => secao.Column(coluna =>
        {
            coluna.Spacing(10);
            coluna.Item().Text($"Rendimentos tributáveis: {Moeda(rendimentosTributaveis)} | INSS: {Moeda(valorInss)} | Pensão: {DescreverPensoes(entrada)}").FontSize(8).FontColor(CinzaTexto);

            foreach (var detalhe in modalidade.Detalhes)
            {
                coluna.Item().Border(1).BorderColor(CinzaBorda).Column(cartao =>
                {
                    cartao.Item().Background(AzulPrimario).Padding(6).Text($"Iteração {detalhe.Sequencia}").FontColor(Colors.White).SemiBold();
                    cartao.Item().Padding(8).Column(memoria =>
                    {
                        memoria.Spacing(3);
                        foreach (var formula in MemoriaCalculoPensao.Iteracao(modalidade, detalhe, entrada, valorInss, Moeda, Percentual))
                            AdicionarFormulaPensao(memoria, formula.Titulo, formula.Formula);
                    });
                });
            }
        }), $"Iterações: {modalidade.Iteracoes} | Total: {Moeda(modalidade.Total)}");
    }

    private static void AdicionarFormulaPensao(ColumnDescriptor coluna, string titulo, string formula)
    {
        coluna.Item().Text(titulo).FontSize(8).SemiBold().FontColor(AzulPrimario);
        coluna.Item().Text(formula).FontFamily("Courier New").FontSize(8);
    }
}
