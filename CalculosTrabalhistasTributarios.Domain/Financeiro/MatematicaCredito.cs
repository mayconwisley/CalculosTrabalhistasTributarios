using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Financeiro;

// Núcleo compartilhado para fluxos mensais Price; entradas validadas pelas calculadoras.
internal static class MatematicaCredito
{
    public static decimal DiasPonderadosIof(int numeroParcelas, DateOnly liberacao, DateOnly primeiroVencimento, decimal taxa)
    {
        // Decreto 6.306/2007, art. 7º, I, b, 2, §§ 1º e 15: PF, 0,0082% ao dia,
        // até 365 dias por amortização, mais 0,38%. Regra consultada em 07/10/2026:
        // https://www.planalto.gov.br/ccivil_03/_ato2007-2010/2007/decreto/d6306compilado.htm
        // Amortizações teóricas Price sem arredondamento intermediário; datas apenas
        // para IOF, sem ajuste por feriados. Juros da simulação continuam mensais.
        var fator = Potencia(1m + taxa, numeroParcelas);
        var parcela = taxa == 0m ? 1m / numeroParcelas : taxa * fator / (fator - 1m);
        var saldo = 1m;
        var ponderacao = 0m;
        for (var i = 0; i < numeroParcelas; i++)
        {
            var amortizacao = i == numeroParcelas - 1 ? saldo : parcela - saldo * taxa;
            var dias = primeiroVencimento.AddMonths(i).DayNumber - liberacao.DayNumber;
            ponderacao += amortizacao * Math.Min(dias, 365);
            saldo -= amortizacao;
        }
        return ponderacao;
    }

    public static (decimal Parcela, decimal Ultima, decimal Total, decimal Juros) Fluxo(decimal principal, decimal taxa, int n, decimal? parcelaLimite = null)
    {
        var fator = Potencia(1m + taxa, n);
        var parcela = parcelaLimite ?? CalculadoraTributacao.Arredondar(taxa == 0m ? principal / n : principal * taxa * fator / (fator - 1m));
        var saldo = principal;
        var total = 0m;
        var jurosTotal = 0m;
        var ultima = parcela;
        for (var i = 1; i <= n; i++)
        {
            var juros = CalculadoraTributacao.Arredondar(saldo * taxa);
            var pagamento = i == n ? saldo + juros : Math.Min(parcela, saldo + juros);
            saldo = saldo + juros - pagamento;
            total += pagamento;
            jurosTotal += juros;
            if (i == n) ultima = pagamento;
        }
        return (parcela, ultima, total, jurosTotal);
    }

    public static decimal Potencia(decimal valor, int expoente)
    {
        var resultado = 1m;
        for (var i = 0; i < expoente; i++) resultado *= valor;
        return resultado;
    }

    public static decimal? TaxaEfetiva(decimal liquido, decimal parcela, decimal ultima, int n)
    {
        decimal Presente(decimal taxa)
        {
            var desconto = 1m;
            var soma = 0m;
            for (var i = 1; i <= n; i++) { desconto /= 1m + taxa; soma += (i == n ? ultima : parcela) * desconto; }
            return soma;
        }
        if (Presente(0m) == liquido) return 0m;
        decimal menor = 0m, maior = 10m;
        if (Presente(maior) > liquido) return null;
        for (var i = 0; i < 80; i++)
        {
            var meio = (menor + maior) / 2m;
            if (Presente(meio) > liquido) menor = meio; else maior = meio;
        }
        return (menor + maior) / 2m;
    }
}
