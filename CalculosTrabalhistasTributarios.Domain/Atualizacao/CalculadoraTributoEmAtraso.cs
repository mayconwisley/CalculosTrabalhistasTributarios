using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Atualizacao;

/// <summary>
/// Acréscimos dos tributos federais pagos depois do vencimento (Lei 9.430/1996, art. 61), que valem também para o DAS
/// do Simples, o DAE do doméstico e a contribuição previdenciária (Lei 8.212/1991, art. 35).
/// </summary>
public static class CalculadoraTributoEmAtraso
{
    /// <summary>0,33% por dia de atraso (art. 61, § 2º).</summary>
    public const decimal MultaPorDia = 0.33m;

    /// <summary>A multa de mora para de crescer em 20% (art. 61, § 2º).</summary>
    public const decimal MultaMaxima = 20m;

    /// <summary>Juros do mês do pagamento (art. 61, § 3º, e art. 5º, § 3º).</summary>
    public const decimal JurosDoMesDoPagamento = 1m;

    /// <param name="selic">Selic acumulada de cada mês, em %.</param>
    public static Result<AcrescimosDeMora> Calcular(decimal principal, DateOnly vencimento, DateOnly pagamento, IReadOnlyDictionary<DateOnly, decimal> selic)
    {
        if (principal <= 0m)
            return Erro.Validacao("Informe o valor principal do tributo.");
        if (pagamento <= vencimento)
            return new AcrescimosDeMora(principal, 0, 0m, 0m, null, null, 0m, 0m, 0m);

        var dias = pagamento.DayNumber - vencimento.DayNumber;
        var percentualMulta = Math.Min(dias * MultaPorDia, MultaMaxima);
        var multa = CalculadoraTributacao.Arredondar(principal * percentualMulta / 100m);

        // A Selic corre do mês seguinte ao vencimento até o anterior ao pagamento; no mês do pagamento, 1%.
        // Pago no próprio mês do vencimento, não há juros.
        var mesVencimento = SerieMensal.MesDe(vencimento);
        var mesPagamento = SerieMensal.MesDe(pagamento);
        if (mesPagamento == mesVencimento)
            return new AcrescimosDeMora(principal, dias, percentualMulta, multa, null, null, 0m, 0m, 0m);

        var primeiro = mesVencimento.AddMonths(1);
        var ultimo = mesPagamento.AddMonths(-1);
        var temSelic = primeiro <= ultimo;
        var somaSelic = temSelic ? SerieMensal.SomaSimples(selic, primeiro, ultimo, "Selic") : 0m;
        if (somaSelic.Falhou)
            return somaSelic.Erro;
        var percentualJuros = somaSelic.Valor + JurosDoMesDoPagamento;
        var juros = CalculadoraTributacao.Arredondar(principal * percentualJuros / 100m);
        return new AcrescimosDeMora(principal, dias, percentualMulta, multa, temSelic ? primeiro : null, temSelic ? ultimo : null, somaSelic.Valor, percentualJuros, juros);
    }
}
