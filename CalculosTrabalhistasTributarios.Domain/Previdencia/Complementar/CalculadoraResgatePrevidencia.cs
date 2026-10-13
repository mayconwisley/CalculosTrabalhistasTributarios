using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;

/// <summary>
/// IR no resgate de PGBL e VGBL. O resgate consome primeiro os aportes mais antigos, e cada parte tem o prazo do seu aporte.
/// Regressivo (Lei 11.053/2004, art. 1º): 35% até 2 anos de acumulação, 30% até 4, 25% até 6, 20% até 8, 15% até 10 e
/// 10% acima de 10 anos, como tributação definitiva. Progressivo (art. 3º): 15% retidos na fonte como antecipação do IR da
/// declaração. A base é o valor resgatado no PGBL e só os rendimentos no VGBL, na proporção do saldo de cada aporte.
/// Desde a Lei 14.803/2024, o regime pode ser escolhido até o primeiro resgate ou benefício.
/// </summary>
public static class CalculadoraResgatePrevidencia
{
    public const int LimiteAportes = 600;
    public const decimal AliquotaProgressiva = 15m;
    private const decimal ValorMaximo = 1_000_000_000m;

    /// <summary>Alíquota regressiva: o prazo até 2 anos inclusive paga 35%, e cada faixa seguinte, 5 pontos a menos.</summary>
    public static decimal AliquotaRegressiva(DateOnly aporte, DateOnly resgate)
    {
        for (var anos = 2; anos <= 10; anos += 2)
            if (resgate <= aporte.AddYears(anos))
                return 35m - (anos - 2) / 2 * 5m;
        return 10m;
    }

    /// <summary>
    /// Saldo estimado de um aporte com rentabilidade mensal constante, pelos meses completos até o resgate. Serve só para
    /// sugerir o saldo de cada aporte quando o extrato não o mostra.
    /// </summary>
    public static decimal EstimarSaldo(decimal aportado, DateOnly data, DateOnly resgate, decimal rentabilidadeMensal)
    {
        var meses = (resgate.Year - data.Year) * 12 + resgate.Month - data.Month - (resgate.Day < data.Day ? 1 : 0);
        var saldo = aportado;
        for (var i = 0; i < Math.Max(0, meses); i++)
            saldo *= 1m + rentabilidadeMensal / 100m;
        return CalculadoraTributacao.Arredondar(saldo);
    }

    public static Result<ApuracaoResgatePrevidencia> Calcular(EntradaResgatePrevidencia entrada)
    {
        if (!Enum.IsDefined(entrada.Tipo) || !Enum.IsDefined(entrada.Regime))
            return Erro.Validacao("Revise o tipo de plano e o regime de tributação.");
        if (entrada.Aportes is null || entrada.Aportes.Count is 0 or > LimiteAportes)
            return Erro.Validacao($"Informe de 1 a {LimiteAportes} aportes do plano.");
        foreach (var aporte in entrada.Aportes)
        {
            var data = aporte.Data.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            if (aporte.Data > entrada.DataResgate)
                return Erro.Validacao($"O aporte de {data} é posterior à data do resgate.");
            if (aporte.Aportado <= 0m || aporte.Saldo < 0m || aporte.Aportado > ValorMaximo || aporte.Saldo > ValorMaximo
                || decimal.Round(aporte.Aportado, 2) != aporte.Aportado || decimal.Round(aporte.Saldo, 2) != aporte.Saldo)
                return Erro.Validacao($"Revise o aporte de {data}: o valor aportado deve ser positivo, e o saldo, zero ou mais, em reais e centavos.");
        }
        var saldoTotal = entrada.Aportes.Sum(aporte => aporte.Saldo);
        if (saldoTotal <= 0m)
            return Erro.Validacao("Os aportes não têm saldo a resgatar.");
        if (entrada.ValorResgate is { } pedido && (pedido <= 0m || pedido > saldoTotal || decimal.Round(pedido, 2) != pedido))
            return Erro.Validacao($"O resgate parcial deve ser positivo e de no máximo o saldo total (R$ {saldoTotal.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}).");

        // Os aportes mais antigos saem primeiro; na mesma data, na ordem informada.
        var restante = entrada.ValorResgate ?? saldoTotal;
        var parcelas = new List<ParcelaResgatada>();
        foreach (var aporte in entrada.Aportes.Select((aporte, ordem) => (aporte, ordem)).OrderBy(item => item.aporte.Data).ThenBy(item => item.ordem).Select(item => item.aporte))
        {
            if (restante == 0m)
                break;
            if (aporte.Saldo == 0m)
                continue;
            var resgatado = Math.Min(restante, aporte.Saldo);
            restante -= resgatado;
            var rendimentos = Math.Max(0m, aporte.Saldo - aporte.Aportado);
            var baseIr = entrada.Tipo == TipoPlanoPrevidencia.Pgbl ? resgatado : CalculadoraTributacao.Arredondar(resgatado * rendimentos / aporte.Saldo);
            var aliquota = AliquotaRegressiva(aporte.Data, entrada.DataResgate);
            parcelas.Add(new ParcelaResgatada(aporte, resgatado, baseIr, aliquota, CalculadoraTributacao.Arredondar(baseIr * aliquota / 100m)));
        }

        var progressivo = CalculadoraTributacao.Arredondar(parcelas.Sum(parcela => parcela.Base) * AliquotaProgressiva / 100m);
        return new ApuracaoResgatePrevidencia(entrada, parcelas, parcelas.Sum(parcela => parcela.Imposto), progressivo);
    }
}
