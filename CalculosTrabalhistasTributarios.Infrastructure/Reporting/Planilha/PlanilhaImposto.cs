using CalculosTrabalhistasTributarios.Application.DTOs;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha.ComponentesPlanilha;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha;

/// <summary>Planilha da simulação tributária: INSS, IRRF nas duas modalidades e as faixas.</summary>
internal static class PlanilhaImposto
{
    internal static Task GerarImpostoAsync(SimulacaoImpostoDto s, string caminhoArquivo, CancellationToken cancellationToken) =>
        SalvarAsync(caminhoArquivo, cancellationToken, pasta =>
        {
            var aba = new Aba(pasta.AddWorksheet("Simulação tributária"));
            aba.Titulo("Simulação tributária", $"Competência {s.Entrada.Competencia:MM/yyyy}");
            aba.Secao("Resumo");
            aba.Par("Valor bruto", Moeda(s.Entrada.ValorBruto));
            aba.Par("Base de INSS considerada", Moeda(s.BaseInssConsiderada));
            aba.Par("Dependentes", s.Entrada.QuantidadeDependentes);
            aba.Par("INSS", Moeda(s.ValorInss));
            aba.Par(s.RetencaoDispensada ? "IRRF (retenção dispensada)" : "IRRF retido", Moeda(s.IrrfAplicado));
            aba.Par("Salário líquido", Moeda(s.SalarioLiquido));
            aba.Par("FGTS padrão (8%)", Moeda(s.FgtsOitoPorCento));
            aba.Par("FGTS jovem aprendiz (2%)", Moeda(s.FgtsDoisPorCento));

            aba.Secao("Comparação entre modalidades");
            aba.Cabecalho("Modalidade", "Base de cálculo", "Alíquota", "Imposto antes da redução", "Redução mensal", "IRRF");
            foreach (var modalidade in s.DescontoSimplificado is null ? new[] { s.Normal } : [s.Normal, s.Simplificada])
                aba.Linha(modalidade.Nome, Moeda(modalidade.BaseCalculo), Percentual(modalidade.Aliquota), Moeda(modalidade.ImpostoAntesReducao), Moeda(modalidade.ReducaoMensal), Moeda(modalidade.Imposto));
            aba.Texto(s.MensagemVantagem);

            Faixas(aba, "INSS por faixas", s.DetalhesInss, s.ValorInss);
            Faixas(aba, "IRRF normal - cálculo progressivo", s.Normal.DetalhesProgressivos, s.Normal.ImpostoAntesReducao);
            if (s.DescontoSimplificado is not null)
                Faixas(aba, "IRRF simplificado - cálculo progressivo", s.Simplificada.DetalhesProgressivos, s.Simplificada.ImpostoAntesReducao);
            aba.Ajustar();
        });

    private static void Faixas(Aba aba, string titulo, IReadOnlyList<DetalheFaixaDto> faixas, decimal total)
    {
        aba.Secao(titulo);
        aba.Cabecalho("Faixa", "Base", "Alíquota", "Imposto");
        foreach (var faixa in faixas)
            aba.Linha($"Faixa {faixa.Faixa}", Moeda(faixa.BaseCalculada), Percentual(faixa.Aliquota), Moeda(faixa.Imposto));
        aba.Linha(true, "Total", "", "", Moeda(total));
    }
}
