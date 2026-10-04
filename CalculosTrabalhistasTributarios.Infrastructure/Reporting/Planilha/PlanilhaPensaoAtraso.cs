using CalculosTrabalhistasTributarios.Application.DTOs;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha.ComponentesPlanilha;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha;

/// <summary>Planilha das parcelas de pensão em atraso, corrigidas e com juros.</summary>
internal static class PlanilhaPensaoAtraso
{
    internal static Task GerarPensaoAtrasoAsync(SimulacaoPensaoAtrasoDto s, string caminhoArquivo, CancellationToken cancellationToken) =>
        SalvarAsync(caminhoArquivo, cancellationToken, pasta =>
        {
            var aba = new Aba(pasta.AddWorksheet("Pensão em atraso"));
            aba.Titulo("Pensão alimentícia em atraso", $"Cálculo em {s.DataCalculo:dd/MM/yyyy}");
            aba.Secao("Resumo do débito");
            aba.Par("Valor devido", Moeda(s.TotalDevido));
            aba.Par("Pagamentos parciais", Moeda(s.TotalPago));
            aba.Par("Saldo original", Moeda(s.TotalSaldo));
            aba.Par($"Correção ({SimulacaoPensaoAtrasoDto.NomeCorrecao(s.Correcao)})", Moeda(s.TotalCorrecao));
            aba.Par("Juros de mora", Moeda(s.TotalJuros));
            aba.Par("Total atualizado", Moeda(s.TotalAtualizado));
            aba.Par($"Rito da prisão ({s.QuantidadePrisao} parcela(s))", Moeda(s.TotalPrisao));
            aba.Par($"Rito da penhora ({s.QuantidadePenhora} parcela(s))", Moeda(s.TotalPenhora));
            if (s.TemAcrescimos)
            {
                aba.Par("Multa de 10% (CPC, art. 523)", Moeda(s.Multa));
                aba.Par("Honorários de 10% (CPC, art. 523)", Moeda(s.Honorarios));
                aba.Par("Total com multa e honorários", Moeda(s.TotalComAcrescimos));
            }

            aba.Secao("Parcelas");
            aba.Cabecalho("Mês", "Vencimento", "Devido", "Pago", "Saldo", "Fator", "Corrigido", "Juros %", "Juros", "Total", "Rito");
            foreach (var p in s.Parcelas)
                aba.Linha(p.Competencia.ToString("MM/yyyy", Cultura), Data(p.Vencimento), Moeda(p.Devido), Moeda(p.Pago), Moeda(p.Saldo), new Celula(p.FatorCorrecao, "0.000000"),
                    Moeda(p.Corrigido), new Celula(p.PercentualJuros / 100m, "0.0000%"), Moeda(p.Juros), Moeda(p.Total), p.RitoPrisao ? "Prisão" : "Penhora");
            aba.Linha(true, "Totais", "", Moeda(s.TotalDevido), Moeda(s.TotalPago), Moeda(s.TotalSaldo), "", Moeda(s.TotalSaldo + s.TotalCorrecao), "", Moeda(s.TotalJuros), Moeda(s.TotalAtualizado), "");

            aba.Secao("Critérios");
            foreach (var criterio in s.Criterios)
                aba.Texto(criterio);
            Observacoes(aba, s.Observacoes);
            aba.Ajustar();
        });
}
