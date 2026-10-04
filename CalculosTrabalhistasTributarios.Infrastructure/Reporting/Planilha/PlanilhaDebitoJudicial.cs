using CalculosTrabalhistasTributarios.Application.DTOs;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha.ComponentesPlanilha;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha;

/// <summary>Planilha da atualização de débitos judiciais.</summary>
internal static class PlanilhaDebitoJudicial
{
    internal static Task GerarDebitoJudicialAsync(SimulacaoDebitoJudicialDto s, string caminhoArquivo, CancellationToken cancellationToken) =>
        SalvarAsync(caminhoArquivo, cancellationToken, pasta =>
        {
            var aba = new Aba(pasta.AddWorksheet("Débito judicial"));
            aba.Titulo($"Atualização de {s.NomeNatureza.ToLowerInvariant()}", $"Cálculo em {s.DataCalculo:dd/MM/yyyy}");
            aba.Secao("Resumo");
            aba.Par("Valor original", Moeda(s.TotalValor));
            aba.Par("Correção", Moeda(s.TotalCorrecao));
            aba.Par("Valor atualizado", Moeda(s.TotalAtualizado));
            aba.Par("Juros", Moeda(s.TotalJuros));
            aba.Par("Total", Moeda(s.Total));
            if (s.TemAcrescimos)
            {
                aba.Par("Multa de 10% (CPC, art. 523)", Moeda(s.Multa));
                aba.Par("Honorários de 10% (CPC, art. 523)", Moeda(s.Honorarios));
                aba.Par("Total com multa e honorários", Moeda(s.TotalComAcrescimos));
            }

            aba.Secao("Parcelas");
            aba.Cabecalho("Descrição", "Vencimento", "Valor", "Fator de correção", "Selic", "Atualizado", "Juros %", "Juros", "Total");
            foreach (var p in s.Parcelas)
                aba.Linha(p.Descricao, p.Vencimento, Moeda(p.Valor), new Celula(p.FatorCorrecao, "0.000000"), new Celula(p.PercentualSelic / 100m, "0.0000%"),
                    Moeda(p.Atualizado), new Celula(p.PercentualJuros / 100m, "0.0000%"), Moeda(p.Juros), Moeda(p.Total));
            aba.Linha(true, "Totais", "", Moeda(s.TotalValor), "", "", Moeda(s.TotalAtualizado), "", Moeda(s.TotalJuros), Moeda(s.Total));

            aba.Secao("Critérios");
            foreach (var criterio in s.Criterios)
                aba.Texto(criterio);
            Observacoes(aba, s.Observacoes);
            aba.Ajustar();
        });
}
