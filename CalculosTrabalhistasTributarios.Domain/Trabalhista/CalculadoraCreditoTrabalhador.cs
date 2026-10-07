using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public static class CalculadoraCreditoTrabalhador
{
    public static Result<ApuracaoCreditoTrabalhador> Calcular(EntradaCreditoTrabalhador e)
    {
        if (!Enum.IsDefined(e.Objetivo) || e.NumeroParcelas is < 1 or > 120)
            return Erro.Validacao("Selecione o objetivo e informe de 1 a 120 parcelas. O limite de 120 é técnico; o prazo aprovado depende do banco.");
        if (e.JurosMensais is < 0m or > 20m || decimal.Round(e.JurosMensais, 6) != e.JurosMensais)
            return Erro.Validacao("Juros mensais: informe de 0 a 20%, com até seis casas decimais, conforme a proposta.");
        if (e.IofAutomatico && (e.DataLiberacao is not { } liberacao || e.PrimeiroVencimento is not { } vencimento ||
            liberacao < new DateOnly(2025, 3, 1) || liberacao.Year > 2100 || vencimento <= liberacao ||
            vencimento > liberacao.AddYears(1)))
            return Erro.Validacao("IOF automático: informe liberação entre 01/03/2025 e 31/12/2100 e primeiro vencimento posterior, até um ano após a liberação. Use a data do contrato, não apenas o mês da folha.");
        if (new[] { e.ValorDesejado, e.IofFinanciado, e.OutrosCustosFinanciados, e.CustosDescontadosNaLiberacao,
            e.RemuneracaoDisponivel, e.ParcelasExistentes }.Any(v => v < 0m || v > 10_000_000m || decimal.Round(v, 2) != v))
            return Erro.Validacao("Valores monetários devem ser não negativos, até R$ 10 milhões e com até duas casas decimais.");
        // Portaria MTE 435/2025, art. 30, com alterações: 35% da remuneração disponível.
        // https://www.gov.br/trabalho-e-emprego/pt-br/assuntos/credito-do-trabalhador/empregador/manual-operacional-do-empregador-credito-do-trabalhador-v2-16-05-25.pdf
        var margem = decimal.Floor(e.RemuneracaoDisponivel * 0.35m * 100m) / 100m;
        if (e.MargemLivreOficial is { } oficial && (oficial < 0m || oficial > 10_000_000m || decimal.Round(oficial, 2) != oficial))
            return Erro.Validacao("Margem livre oficial: informe valor não negativo, com até duas casas decimais, ou deixe vazio para estimar.");
        var livre = e.MargemLivreOficial ?? Math.Max(0m, margem - e.ParcelasExistentes);
        var taxa = e.JurosMensais / 100m;
        var diasPonderados = e.IofAutomatico ? DiasPonderadosIof(e, taxa) : 0m;
        var coeficienteIof = e.IofAutomatico ? 0.0038m + 0.000082m * diasPonderados : 0m;
        var iof = e.IofFinanciado;
        if (e.IofAutomatico)
        {
            // IOF financiado integra o principal: P = crédito + outros custos + IOF(P).
            // Resolve com precisão decimal antes do arredondamento monetário do imposto.
            var baseSemIof = e.ValorDesejado + e.OutrosCustosFinanciados;
            iof = CalculadoraTributacao.Arredondar(baseSemIof * coeficienteIof / (1m - coeficienteIof));
        }
        var custos = iof + e.OutrosCustosFinanciados;
        var valor = e.ValorDesejado;
        if (e.Objetivo == ObjetivoCreditoTrabalhador.LimitePelaMargem)
        {
            if (livre == 0m)
                return Erro.Validacao("Não há margem livre estimada. Confira a remuneração disponível e as parcelas existentes.");
            // Reconstrói o saldo de trás para frente, respeitando juros arredondados por mês.
            // Evita busca binária sobre prestações cuja última parcela pode oscilar por centavos.
            var financiavel = 0m;
            for (var i = 0; i < e.NumeroParcelas; i++)
            {
                var alvo = financiavel + livre;
                var anterior = decimal.Floor(alvo / (1m + taxa) * 100m) / 100m;
                while (anterior + CalculadoraTributacao.Arredondar(anterior * taxa) > alvo) anterior -= 0.01m;
                while (anterior + 0.01m + CalculadoraTributacao.Arredondar((anterior + 0.01m) * taxa) <= alvo) anterior += 0.01m;
                financiavel = anterior;
            }
            if (e.IofAutomatico)
            {
                iof = CalculadoraTributacao.Arredondar(financiavel * coeficienteIof);
                custos = iof + e.OutrosCustosFinanciados;
            }
            valor = financiavel - custos;
            if (valor > 10_000_000m)
                return Erro.Validacao("O valor pela margem excede o limite técnico de R$ 10 milhões. Reduza o prazo ou simule um valor desejado.");
        }
        if (valor <= 0m || e.CustosDescontadosNaLiberacao >= valor)
            return Erro.Validacao("O crédito líquido deve ser positivo. Confira valor desejado, margem e custos descontados na liberação.");
        var principal = valor + custos;
        var f = Fluxo(principal, taxa, e.NumeroParcelas, e.Objetivo == ObjetivoCreditoTrabalhador.LimitePelaMargem ? livre : null);
        if (f.Parcela <= 0m || f.Ultima <= 0m)
            return Erro.Validacao("O valor é insuficiente para o prazo escolhido. Aumente o crédito ou reduza as parcelas.");
        var liquido = valor - e.CustosDescontadosNaLiberacao;
        var cet = TaxaEfetiva(liquido, f.Parcela, f.Ultima, e.NumeroParcelas);
        return new ApuracaoCreditoTrabalhador(margem, livre, valor, principal, liquido, f.Parcela, f.Ultima,
            f.Total, f.Juros, f.Total - liquido, (Potencia(1m + taxa, 12) - 1m) * 100m,
            cet is { } c ? c * 100m : null, Math.Max(f.Parcela, f.Ultima) <= livre, iof, principal * diasPonderados);
    }

    private static decimal DiasPonderadosIof(EntradaCreditoTrabalhador e, decimal taxa)
    {
        // Decreto 6.306/2007, art. 7º, I, b, 2, §§ 1º e 15: PF, 0,0082% ao dia,
        // até 365 dias por amortização, mais 0,38%. Regra consultada em 07/10/2026:
        // https://www.planalto.gov.br/ccivil_03/_ato2007-2010/2007/decreto/d6306compilado.htm
        // Amortizações teóricas Price sem arredondamento intermediário; datas apenas
        // para IOF, sem ajuste por feriados. Juros da simulação continuam mensais.
        var fator = Potencia(1m + taxa, e.NumeroParcelas);
        var parcela = taxa == 0m ? 1m / e.NumeroParcelas : taxa * fator / (fator - 1m);
        var saldo = 1m;
        var ponderacao = 0m;
        for (var i = 0; i < e.NumeroParcelas; i++)
        {
            var amortizacao = i == e.NumeroParcelas - 1 ? saldo : parcela - saldo * taxa;
            var dias = e.PrimeiroVencimento.GetValueOrDefault().AddMonths(i).DayNumber - e.DataLiberacao.GetValueOrDefault().DayNumber;
            ponderacao += amortizacao * Math.Min(dias, 365);
            saldo -= amortizacao;
        }
        return ponderacao;
    }

    private static (decimal Parcela, decimal Ultima, decimal Total, decimal Juros) Fluxo(decimal principal, decimal taxa, int n, decimal? parcelaLimite = null)
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

    private static decimal? TaxaEfetiva(decimal liquido, decimal parcela, decimal ultima, int n)
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
