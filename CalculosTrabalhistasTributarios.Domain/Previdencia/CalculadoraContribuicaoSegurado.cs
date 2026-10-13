using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Domain.Previdencia;

/// <summary>
/// Contribuição do contribuinte individual e do facultativo (Lei 8.212/1991, art. 21): 20% do salário de contribuição
/// no plano normal, entre o salário mínimo e o teto; 11% do salário mínimo no plano simplificado (§ 2º, I); 5% do salário
/// mínimo no facultativo de baixa renda (§ 2º, II, "b"). Os planos reduzidos não contam para a aposentadoria por tempo de
/// contribuição nem para a certidão de tempo de contribuição sem a complementação da diferença até 20% (§ 3º).
/// O vencimento é o dia 15 do mês seguinte, adiado para o dia útil seguinte sem expediente bancário (art. 30, II);
/// quem contribui sobre o salário mínimo pode recolher por trimestre civil (Decreto 3.048/1999, art. 216, § 15).
/// </summary>
public static class CalculadoraContribuicaoSegurado
{
    public const decimal AliquotaNormal = 20m;
    public const decimal AliquotaSimplificada = 11m;
    public const decimal AliquotaBaixaRenda = 5m;

    /// <summary>A competência, no mensal, ou os três meses do trimestre civil que a contém.</summary>
    public static IReadOnlyList<DateOnly> MesesDoPeriodo(DateOnly competencia, PeriodicidadeContribuicao periodicidade)
    {
        var mes = new DateOnly(competencia.Year, competencia.Month, 1);
        if (periodicidade == PeriodicidadeContribuicao.Mensal)
            return [mes];
        var inicio = new DateOnly(mes.Year, (mes.Month - 1) / 3 * 3 + 1, 1);
        return [inicio, inicio.AddMonths(1), inicio.AddMonths(2)];
    }

    public static Result<ApuracaoContribuicaoSegurado> Calcular(EntradaContribuicaoSegurado entrada)
    {
        if (!Enum.IsDefined(entrada.Categoria) || !Enum.IsDefined(entrada.Plano) || !Enum.IsDefined(entrada.Periodicidade))
            return Erro.Validacao("Revise a categoria, o plano e a periodicidade da contribuição.");
        var esperados = entrada.Periodicidade == PeriodicidadeContribuicao.Mensal ? 1 : 3;
        if (entrada.Meses is null || entrada.Meses.Count != esperados || entrada.Meses.Any(mes => mes.SalarioMinimo <= 0m || mes.Teto < mes.SalarioMinimo))
            return Erro.Validacao("Faltam o salário mínimo e o teto do INSS das competências do período.");
        if (entrada.Plano == PlanoContribuicao.BaixaRenda && entrada.Categoria != CategoriaSegurado.Facultativo)
            return Erro.Validacao("O plano de 5% do salário mínimo é só do facultativo de baixa renda. O microempreendedor individual paga os 5% no DAS do MEI.");
        if (entrada.Plano == PlanoContribuicao.Normal && entrada.SalarioContribuicao < entrada.Meses[0].SalarioMinimo)
            return Erro.Validacao($"No plano normal, o salário de contribuição é de no mínimo um salário mínimo (R$ {entrada.Meses[0].SalarioMinimo.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"))}); abaixo dele, a contribuição não conta (Constituição, art. 195, § 14).");
        if (decimal.Round(entrada.SalarioContribuicao, 2) != entrada.SalarioContribuicao || entrada.SalarioContribuicao > 10_000_000m)
            return Erro.Validacao("Revise o salário de contribuição: informe um valor em reais e centavos.");
        // O trimestral é para quem contribui sobre o salário mínimo; no plano normal, a base precisa ser o próprio mínimo.
        if (entrada.Periodicidade == PeriodicidadeContribuicao.Trimestral && entrada.Plano == PlanoContribuicao.Normal
            && entrada.Meses.Any(mes => mes.SalarioMinimo != entrada.SalarioContribuicao))
            return Erro.Validacao("O recolhimento trimestral só vale para quem contribui sobre o salário mínimo (Decreto 3.048/1999, art. 216, § 15). Use o mensal ou informe o salário mínimo como base.");

        var aliquota = entrada.Plano switch
        {
            PlanoContribuicao.Normal => AliquotaNormal,
            PlanoContribuicao.Simplificado => AliquotaSimplificada,
            _ => AliquotaBaixaRenda
        };
        var meses = entrada.Meses.Select(mes =>
        {
            var limitada = entrada.Plano == PlanoContribuicao.Normal && entrada.SalarioContribuicao > mes.Teto;
            var baseMes = entrada.Plano == PlanoContribuicao.Normal ? Math.Min(entrada.SalarioContribuicao, mes.Teto) : mes.SalarioMinimo;
            var complementacao = entrada.Plano == PlanoContribuicao.Normal ? 0m : CalculadoraTributacao.Arredondar(mes.SalarioMinimo * (AliquotaNormal - aliquota) / 100m);
            return new ContribuicaoDoMes(mes, baseMes, aliquota, CalculadoraTributacao.Arredondar(baseMes * aliquota / 100m), limitada, complementacao);
        }).ToArray();

        var ultimo = entrada.Meses[^1].Competencia;
        return new ApuracaoContribuicaoSegurado(entrada, meses, Codigo(entrada), CodigoComplementacao(entrada), Vencimento(ultimo));
    }

    /// <summary>Dia 15 do mês seguinte; no sábado ou no domingo, a segunda-feira. Feriados não são considerados.</summary>
    public static DateOnly Vencimento(DateOnly ultimaCompetencia)
    {
        var dia = new DateOnly(ultimaCompetencia.Year, ultimaCompetencia.Month, 15).AddMonths(1);
        return dia.DayOfWeek switch
        {
            DayOfWeek.Saturday => dia.AddDays(2),
            DayOfWeek.Sunday => dia.AddDays(1),
            _ => dia
        };
    }

    private static string Codigo(EntradaContribuicaoSegurado e)
    {
        var mensal = e.Periodicidade == PeriodicidadeContribuicao.Mensal;
        return (e.Categoria, e.Plano) switch
        {
            (CategoriaSegurado.ContribuinteIndividual, PlanoContribuicao.Normal) => mensal ? "1007" : "1104",
            (CategoriaSegurado.ContribuinteIndividual, _) => mensal ? "1163" : "1180",
            (_, PlanoContribuicao.Normal) => mensal ? "1406" : "1457",
            (_, PlanoContribuicao.Simplificado) => mensal ? "1473" : "1490",
            _ => mensal ? "1929" : "1937"
        };
    }

    private static string? CodigoComplementacao(EntradaContribuicaoSegurado e)
    {
        if (e.Plano != PlanoContribuicao.Simplificado)
            return null;
        var mensal = e.Periodicidade == PeriodicidadeContribuicao.Mensal;
        return e.Categoria == CategoriaSegurado.ContribuinteIndividual ? (mensal ? "1295" : "1198") : (mensal ? "1686" : "1694");
    }
}
