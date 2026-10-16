using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Atualizacao;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Previdencia;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>
/// Média dos salários de contribuição corrigidos pelo INPC da tabela de índices, comum à aposentadoria e à pensão por morte:
/// apuração, limite pelo tempo de contribuição, memória por período e observações.
/// </summary>
public static class MemoriaMediaSalarios
{
    /// <param name="dataReferencia">Data da simulação ou do óbito: os salários são corrigidos até o mês anterior a ela.</param>
    public static async Task<Result<ApuracaoMediaSalarios>> CalcularAsync(IIndicesEconomicos indices, IReadOnlyList<PeriodoSalarioContribuicao> salarios,
        DateOnly dataReferencia, int mesesContribuicao, CancellationToken cancellationToken)
    {
        var media = CalculadoraMediaSalarios.Calcular(salarios, dataReferencia, await indices.ObterAsync(IndiceEconomico.Inpc, cancellationToken));
        if (media.Falhou)
            return media.Erro;
        if (media.Valor.Quantidade > mesesContribuicao)
            return Erro.Validacao($"Os períodos somam {Tempo(media.Valor.Quantidade)} de salários, mais que o tempo de contribuição informado ({Tempo(mesesContribuicao)}): revise o tempo ou os períodos.");
        return media.Valor;
    }

    public static GrupoMemoriaDto Memoria(ApuracaoMediaSalarios m)
    {
        var formulas = m.Periodos.OrderBy(periodo => periodo.Inicio).Select(periodo =>
        {
            var meses = m.Meses.Where(mes => mes.Competencia >= SerieMensal.MesDe(periodo.Inicio) && mes.Competencia <= SerieMensal.MesDe(periodo.Fim)).ToArray();
            var nome = meses.Length == 1 ? Formato.Competencia(meses[0].Competencia) : $"{Formato.Competencia(meses[0].Competencia)} a {Formato.Competencia(meses[^1].Competencia)}";
            var fatores = meses[0].Fator == meses[^1].Fator ? $"fator {Fator(meses[0].Fator)}" : $"fatores de {Fator(meses[0].Fator)} a {Fator(meses[^1].Fator)}";
            return new FormulaDto(nome, $"{meses.Length} {(meses.Length == 1 ? "mês" : "meses")} de {Formato.Moeda(periodo.Salario)} x {fatores} = {Formato.Moeda(meses.Sum(mes => mes.Corrigido))}");
        }).ToList();
        formulas.Add(new("Correção", m.CorrigidoAte is { } ate
            ? $"Fator da competência: produto de (1 + INPC do mês) dela até {Formato.Competencia(ate)}; cada salário corrigido é arredondado aos centavos."
            : "Sem correção: os salários são de meses ainda sem INPC publicado."));
        formulas.Add(new("Média", $"{Formato.Moeda(m.Soma)} ÷ {m.Quantidade} competências = {Formato.Moeda(m.Media)}"));
        return new GrupoMemoriaDto("Média dos salários de contribuição", Formato.Moeda(m.Media), formulas);
    }

    public static string Observacao(ApuracaoMediaSalarios m) =>
        $"Média calculada com {m.Quantidade} salários de contribuição desde {Formato.Competencia(m.Meses[0].Competencia)}, cada um corrigido pelo INPC{(m.CorrigidoAte is { } ate ? $" até {Formato.Competencia(ate)}" : "")} (EC 103/2019, art. 26; Lei 8.213/1991, art. 29-B). Os salários entram como informados, já limitados ao teto da época, como no CNIS; desde 11/2019, a competência abaixo do salário mínimo só conta se for complementada (EC 103/2019, art. 29). A exclusão das contribuições que reduzem a média (art. 26, § 6º) não é aplicada. Confira os salários e o tempo no CNIS e no Meu INSS.";

    /// <summary>Aviso quando os últimos meses ficaram sem correção por falta do INPC publicado; nulo se não faltou.</summary>
    public static string? AvisoInpc(ApuracaoMediaSalarios m) => m.FaltaInpcDesde is { } desde
        ? $"O INPC de {Formato.Competencia(desde)} em diante ainda não está na tabela de índices: os salários foram corrigidos só até o último mês publicado. Atualize a tabela INPC quando o IBGE divulgar o índice."
        : null;

    private static string Fator(decimal fator) => Math.Round(fator, 6).ToString("0.000000", Formato.Cultura);

    private static string Tempo(int meses) => meses % 12 == 0 ? $"{meses / 12} anos" : $"{meses / 12} anos e {meses % 12} {(meses % 12 == 1 ? "mês" : "meses")}";
}
