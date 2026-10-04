using CalculosTrabalhistasTributarios.Application.DTOs;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha.ComponentesPlanilha;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha;

/// <summary>Planilha da indenização do período de estabilidade.</summary>
internal static class PlanilhaEstabilidade
{
    internal static Task GerarEstabilidadeAsync(SimulacaoEstabilidadeDto s, EntradaEstabilidadeDto entrada, string caminhoArquivo, CancellationToken cancellationToken) =>
        SalvarAsync(caminhoArquivo, cancellationToken, pasta =>
        {
            var aba = new Aba(pasta.AddWorksheet("Estabilidade"));
            aba.Titulo("Cálculo de estabilidade", $"Demissão em {entrada.Demissao:dd/MM/yyyy}");
            aba.Secao("Dados considerados");
            aba.Par("Média remuneratória", Moeda(entrada.MediaRemuneratoria));
            aba.Par("Dias-base", entrada.DiasBase);
            aba.Par("Data de demissão", Data(entrada.Demissao));
            aba.Par("Fim da estabilidade", Data(entrada.FimEstabilidade));
            aba.Par("Dias de estabilidade", s.DiasEstabilidade);
            aba.Par("Meses e dias", $"{s.Meses} mês(es) e {s.DiasAlemDosMeses} dia(s)");

            aba.Secao("Verbas");
            aba.Cabecalho("Verba", "Referência", "Valor");
            aba.Linha("Indenização do período", $"{s.Meses} mês(es) e {s.DiasAlemDosMeses} dia(s)", Moeda(s.Indenizacao));
            aba.Linha("13º salário", $"{s.AvosDecimoTerceiro}/12", Moeda(s.DecimoTerceiro));
            aba.Linha("Férias", $"{s.AvosFerias}/12", Moeda(s.Ferias));
            aba.Linha("1/3 de férias", "", Moeda(s.TercoFerias));
            aba.Linha("FGTS", "8%", Moeda(s.FgtsOitoPorCento));
            aba.Linha("Multa do FGTS", "40%", Moeda(s.MultaFgtsQuarentaPorCento));
            aba.Linha("Complementos", "", Moeda(s.Complementos));
            aba.Linha(true, "Total estimado", "", Moeda(s.Total));
            aba.Ajustar();
        });
}
