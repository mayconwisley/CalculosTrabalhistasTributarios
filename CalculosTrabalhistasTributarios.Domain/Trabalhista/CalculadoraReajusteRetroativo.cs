using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public static class CalculadoraReajusteRetroativo
{
    public static Result<ApuracaoReajusteRetroativo> Calcular(IReadOnlyList<ParcelaReajusteRetroativo>? parcelas)
    {
        if (parcelas is null || parcelas.Count == 0 || !parcelas.Any(item => item.Tipo == TipoParcelaReajuste.Salario))
            return Erro.Validacao("Gere ou informe pelo menos uma competência de salário.");
        if (parcelas.Count > 130)
            return Erro.Validacao("O demonstrativo aceita até 120 meses de salário e 10 lançamentos de 13º ou férias.");

        var apuradas = new List<DiferencaParcelaReajuste>(parcelas.Count);
        var meses = new HashSet<DateOnly>();
        foreach (var parcela in parcelas)
        {
            if (!Enum.IsDefined(parcela.Tipo) || parcela.Competencia.Day != 1 || parcela.BasePaga < 0m
                || parcela.BaseDevida < 0m || parcela.BasePaga > 1_000_000_000m || parcela.BaseDevida > 1_000_000_000m
                || !Centavos(parcela.BasePaga) || !Centavos(parcela.BaseDevida))
                return Erro.Validacao("Revise o tipo, a competência e os valores monetários dos lançamentos.");
            if (parcela.Tipo == TipoParcelaReajuste.Salario && (!meses.Add(parcela.Competencia) || parcela.Quantidade != 1))
                return Erro.Validacao("Cada competência de salário deve aparecer uma vez.");
            if (parcela.Tipo != TipoParcelaReajuste.Salario && (parcela.BasePaga <= 0m || parcela.BaseDevida <= 0m))
                return Erro.Validacao("Nos lançamentos de 13º e férias já pagos, informe a base salarial paga e a base correta.");
            if (parcela.Tipo == TipoParcelaReajuste.DecimoTerceiro && parcela.Quantidade is < 1 or > 12
                || parcela.Tipo == TipoParcelaReajuste.FeriasGozadas && parcela.Quantidade is < 1 or > 30)
                return Erro.Validacao("Informe de 1 a 12 avos de 13º ou de 1 a 30 dias de férias gozadas.");

            var pago = Valor(parcela, parcela.BasePaga);
            var devido = Valor(parcela, parcela.BaseDevida);
            if (devido < pago)
                return Erro.Validacao($"Em {parcela.Competencia:MM/yyyy}, o valor devido não pode ser menor que o já pago.");
            var diferenca = devido - pago;
            apuradas.Add(new(parcela, pago, devido, diferenca, Arredondar(diferenca * .08m)));
        }
        if (meses.Count > 120 || parcelas.Count - meses.Count > 10)
            return Erro.Validacao("O demonstrativo aceita até 120 meses de salário e 10 lançamentos de 13º ou férias.");
        return new ApuracaoReajusteRetroativo(apuradas);
    }

    private static decimal Valor(ParcelaReajusteRetroativo parcela, decimal baseSalarial) => parcela.Tipo switch
    {
        TipoParcelaReajuste.Salario => baseSalarial,
        TipoParcelaReajuste.DecimoTerceiro => Arredondar(baseSalarial * parcela.Quantidade / 12m),
        TipoParcelaReajuste.FeriasGozadas => Ferias(baseSalarial, parcela.Quantidade),
        _ => 0m
    };

    private static decimal Ferias(decimal baseSalarial, int dias)
    {
        var remuneracao = Arredondar(baseSalarial * dias / 30m);
        return remuneracao + Arredondar(remuneracao / 3m);
    }

    private static bool Centavos(decimal valor) => decimal.Round(valor, 2) == valor;
    private static decimal Arredondar(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
}
