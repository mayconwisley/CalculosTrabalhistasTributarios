using CalculosTrabalhistasTributarios.Application.DTOs;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha.ComponentesPlanilha;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha;

/// <summary>Planilha da pensão alimentícia, com as modalidades do IRRF e a memória de cada beneficiário.</summary>
internal static class PlanilhaPensao
{
    internal static Task GerarPensaoAsync(SimulacaoPensaoDto s, EntradaPensaoDto entrada, string caminhoArquivo, CancellationToken cancellationToken) =>
        SalvarAsync(caminhoArquivo, cancellationToken, pasta =>
        {
            var aplicada = s.Aplicada;
            var aba = new Aba(pasta.AddWorksheet("Pensão alimentícia"));
            aba.Titulo("Cálculo de pensão alimentícia", $"Competência {entrada.Competencia:MM/yyyy}");
            aba.Secao("Dados de quem paga");
            aba.Par("Valor bruto", Moeda(entrada.ValorBruto));
            aba.Par("Base de INSS", Moeda(entrada.BaseInss));
            aba.Par("Dependentes", entrada.Dependentes);
            aba.Par("Outros descontos", Moeda(entrada.OutrosDescontos));
            if (entrada.Beneficiarios.Count > 1)
                aba.Par("Ordem de cálculo", entrada.Sucessiva ? "Cada uma após descontar as anteriores" : "Todas sobre a mesma base");

            aba.Secao("Resumo");
            aba.Par("Pensões", Moeda(aplicada.Pensao));
            aba.Par("INSS", Moeda(s.ValorInss));
            if (s.FaixasInss.Count > 0)
                aba.Par("Alíquota da faixa do INSS", Percentual(s.FaixasInss[^1].Aliquota));
            aba.Par($"IRRF com {(entrada.Beneficiarios.Count > 1 ? "pensões" : "pensão")}", Moeda(aplicada.Imposto));
            aba.Par("Alíquota da faixa do IRRF com pensão", Percentual(aplicada.Detalhes[^1].Aliquota));
            aba.Par("IRRF sem pensão", Moeda(s.ImpostoSemPensao));
            aba.Par("Alíquota da faixa do IRRF sem pensão", Percentual(s.AliquotaIrrfSemPensao));
            aba.Par("Economia de IRRF", Moeda(s.EconomiaIrrf));
            aba.Par("Líquido de quem paga", Moeda(entrada.ValorBruto - entrada.OutrosDescontos - s.ValorInss - aplicada.Imposto - aplicada.Pensao));
            aba.Texto(MemoriaCalculoPensao.Explicacao(s, entrada, MoedaTexto, PercentualTexto));
            aba.Texto(MemoriaCalculoPensao.Faixas(s, MoedaTexto));

            aba.Secao("Pensão por beneficiário");
            aba.Cabecalho("Beneficiário", "Regra", "Base", "Pensão");
            foreach (var pensao in aplicada.Detalhes[^1].Beneficiarios)
                aba.Linha(pensao.Nome, pensao.Regra.Descrever(MoedaTexto, PercentualTexto), Moeda(pensao.Base), Moeda(pensao.Pensao));

            aba.Secao("Comparação entre modalidades");
            aba.Cabecalho("Modalidade", "Alíquota da faixa do IRRF", "IRRF final", "Pensão", "IRRF + pensão", "Iterações");
            foreach (var modalidade in s.Modalidades)
                aba.Linha(modalidade.Nome, Percentual(modalidade.Detalhes[^1].Aliquota), Moeda(modalidade.Imposto), Moeda(modalidade.Pensao), Moeda(modalidade.Total), modalidade.Iteracoes);
            aba.Texto(s.MensagemVantagem);
            aba.Ajustar();

            var memoria = new Aba(pasta.AddWorksheet("Memória de cálculo"));
            memoria.Titulo("Memória de cálculo por iteração", $"Competência {entrada.Competencia:MM/yyyy}");
            foreach (var modalidade in s.Modalidades)
            {
                memoria.Secao($"{modalidade.Nome} - {modalidade.Iteracoes} iteração(ões)");
                memoria.Cabecalho("Iteração", "Base do IRRF", "Alíquota", "IRRF", "Base da pensão", "Pensão");
                foreach (var iteracao in modalidade.Detalhes)
                    memoria.Linha(iteracao.Sequencia, Moeda(iteracao.BaseIrrf), Percentual(iteracao.Aliquota), Moeda(iteracao.Imposto), Moeda(iteracao.BasePensao), Moeda(iteracao.Pensao));
                foreach (var formula in MemoriaCalculoPensao.Iteracao(modalidade, modalidade.Detalhes[^1], entrada, s.ValorInss, MoedaTexto, PercentualTexto))
                    memoria.Formula(formula.Titulo, formula.Formula);
            }
            memoria.Ajustar();
        });

    private static string MoedaTexto(decimal valor) => valor.ToString("C2", Cultura);

    private static string PercentualTexto(decimal valor) => valor.ToString("N2", Cultura) + "%";
}
