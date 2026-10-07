using CalculosTrabalhistasTributarios.Application.DTOs;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha.ComponentesPlanilha;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha;

/// <summary>Planilha do demonstrativo das calculadoras: resumo, proventos, descontos e memória de cálculo.</summary>
internal static class PlanilhaDemonstrativo
{
    internal static Task GerarDemonstrativoAsync(DemonstrativoDto d, string caminhoArquivo, CancellationToken cancellationToken) =>
        SalvarAsync(caminhoArquivo, cancellationToken, pasta =>
        {
            var aba = new Aba(pasta.AddWorksheet(NomeDaAba(d.Titulo)));
            aba.Titulo(d.Titulo, d.Referencia);
            aba.Secao("Resumo executivo");
            aba.Cabecalho("Indicador", "Valor", "Complemento");
            foreach (var destaque in d.Destaques)
                aba.Linha(destaque.Rotulo, destaque.Valor, destaque.Complemento);

            if (d.Comparativo is { } comparativo)
            {
                aba.Secao(comparativo.Titulo);
                aba.Cabecalho(["Descrição", .. comparativo.Colunas]);
                foreach (var linha in comparativo.Linhas)
                    aba.Linha(linha.Destaque, [linha.Descricao, .. linha.Valores]);
            }

            if (d.TemVerbas)
            {
                aba.Secao("Demonstrativo");
                aba.Cabecalho("Descrição", "Referência", d.RotuloProventos, "Descontos");
                foreach (var verba in d.Proventos)
                    aba.Linha(verba.Descricao, verba.Referencia, Moeda(verba.Valor), "");
                foreach (var verba in d.Descontos)
                    aba.Linha(verba.Descricao, verba.Referencia, "", Moeda(verba.Valor));
                aba.Linha(true, "Totais", "", Moeda(d.TotalProventos), Moeda(d.TotalDescontos));
                aba.Linha(true, d.RotuloResultado, "", Moeda(d.Resultado), "");
            }

            if (d.Informativos.Count > 0)
            {
                aba.Secao("Valores informativos (não entram nos totais nem no resultado)");
                aba.Cabecalho("Descrição", "Referência", "Valor");
                foreach (var verba in d.Informativos)
                    aba.Linha(verba.Descricao, verba.Referencia, Moeda(verba.Valor));
            }
            Observacoes(aba, d.Observacoes);
            aba.Ajustar();

            if (d.Memoria.Count > 0)
            {
                var memoria = new Aba(pasta.AddWorksheet("Memória de cálculo"));
                memoria.Titulo("Memória de cálculo", d.Referencia);
                foreach (var grupo in d.Memoria)
                {
                    memoria.Secao(string.IsNullOrEmpty(grupo.Destaque) ? grupo.Titulo : $"{grupo.Titulo} - {grupo.Destaque}");
                    foreach (var formula in grupo.Formulas)
                        memoria.Formula(formula.Titulo, formula.Formula);
                }
                memoria.Ajustar();
            }
        });

    /// <summary>O Excel aceita até 31 caracteres no nome da aba, sem barras, dois-pontos, colchetes, asterisco e interrogação.</summary>
    private static string NomeDaAba(string titulo)
    {
        var nome = new string(titulo.Where(caractere => !"/\\:[]*?".Contains(caractere)).ToArray()).Trim();
        return nome.Length > 31 ? nome[..31] : nome;
    }
}
