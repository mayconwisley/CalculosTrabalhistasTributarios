using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// Imposto sobre o ganho de capital da pessoa física na venda de bens: as isenções, as reduções dos imóveis e as alíquotas
/// progressivas de 15% a 22,5% (Lei 8.981/1995, art. 21, com a redação da Lei 13.259/2016).
/// </summary>
public static class CalculadoraGanhoDeCapital
{
    /// <summary>Vendas de bens da mesma natureza de até R$ 35.000,00 no mês são isentas (Lei 9.250/1995, art. 22).</summary>
    public const decimal LimitePequenoValor = 35_000m;

    /// <summary>Único imóvel vendido por até R$ 440.000,00, sem outra venda de imóvel em 5 anos (Lei 9.250/1995, art. 23).</summary>
    public const decimal LimiteImovelUnico = 440_000m;

    private static readonly (decimal Limite, decimal Aliquota)[] Aliquotas =
    [
        (5_000_000m, 15m),
        (10_000_000m, 17.5m),
        (30_000_000m, 20m),
        (decimal.MaxValue, 22.5m)
    ];

    private static readonly DateOnly InicioFr1 = new(1996, 1, 1);
    private static readonly DateOnly FimFr1 = new(2005, 11, 1);
    private static readonly DateOnly InicioFr2 = new(2005, 12, 1);

    public static Result<ApuracaoGanhoDeCapital> Calcular(AlienacaoDeBem a)
    {
        if (a.Venda <= 0m || a.Custo < 0m || a.DespesasVenda < 0m || a.Reinvestido < 0m || a.VendasNoMes < 0m)
            return Erro.Validacao("Informe o valor da venda; os demais valores não podem ser negativos.");
        if (a.Alienacao < a.Aquisicao)
            return Erro.Validacao("A data da venda não pode ser anterior à da compra.");

        var ganho = Math.Max(0m, a.Venda - a.DespesasVenda - a.Custo);
        if (ganho == 0m)
            return Isento(0m, "Não houve ganho: o valor da venda, menos as despesas, não passou do custo de aquisição.");
        if (Math.Max(a.VendasNoMes, a.Venda) <= LimitePequenoValor)
            return Isento(ganho, "Isento: as vendas de bens da mesma natureza no mês não passaram de R$ 35.000,00 (Lei 9.250/1995, art. 22).");
        if (a.Imovel && a.UnicoImovel && a.Venda <= LimiteImovelUnico)
            return Isento(ganho, "Isento: único imóvel, vendido por até R$ 440.000,00, sem outra venda de imóvel nos 5 anos anteriores (Lei 9.250/1995, art. 23).");

        var reducao1988 = a.Imovel ? PercentualReducao1988(a.Aquisicao.Year) : 0m;
        var tributavel = ganho * (1m - reducao1988 / 100m);
        int mesesFr1 = 0, mesesFr2 = 0;
        decimal fr1 = 1m, fr2 = 1m;
        // Fatores de redução dos imóveis (Lei 11.196/2005, art. 40; IN SRF 599/2005, art. 3º), aplicados um depois do outro.
        if (a.Imovel)
        {
            var mesCompra = new DateOnly(a.Aquisicao.Year, a.Aquisicao.Month, 1);
            var mesVenda = new DateOnly(a.Alienacao.Year, a.Alienacao.Month, 1);
            if (mesCompra <= FimFr1)
            {
                mesesFr1 = Meses(Maior(InicioFr1, mesCompra), Menor(FimFr1, mesVenda));
                fr1 = Fator(1.0060, mesesFr1);
            }
            if (mesVenda >= InicioFr2)
            {
                mesesFr2 = Meses(Maior(InicioFr2, mesCompra), mesVenda);
                fr2 = Fator(1.0035, mesesFr2);
            }
            tributavel *= fr1 * fr2;
        }

        // Venda de imóvel residencial aplicada em outro imóvel residencial em 180 dias: isenta na proporção aplicada (Lei 11.196/2005, art. 39).
        var proporcao = a.Bem == BemAlienado.ImovelResidencial && a.Reinvestido > 0m ? Math.Min(1m, a.Reinvestido / a.Venda) : 0m;
        tributavel = CalculadoraTributacao.Arredondar(tributavel * (1m - proporcao));

        return new ApuracaoGanhoDeCapital(ganho, null, reducao1988, mesesFr1, fr1, mesesFr2, fr2, proporcao, tributavel, Progressivo(tributavel));
    }

    /// <summary>Imóvel comprado até 1969 não tem imposto; de 1970 a 1988, a redução cai 5 pontos por ano, de 95% a 5%.</summary>
    public static decimal PercentualReducao1988(int anoAquisicao) => anoAquisicao <= 1969 ? 100m : anoAquisicao <= 1988 ? (1988 - anoAquisicao + 1) * 5m : 0m;

    private static IReadOnlyList<(decimal Parcela, decimal Aliquota, decimal Imposto)> Progressivo(decimal tributavel)
    {
        var faixas = new List<(decimal, decimal, decimal)>();
        var anterior = 0m;
        foreach (var (limite, aliquota) in Aliquotas)
        {
            if (tributavel <= anterior)
                break;
            var parcela = Math.Min(tributavel, limite) - anterior;
            faixas.Add((parcela, aliquota, CalculadoraTributacao.Arredondar(parcela * aliquota / 100m)));
            anterior = limite;
        }
        return faixas;
    }

    private static ApuracaoGanhoDeCapital Isento(decimal ganho, string motivo) => new(ganho, motivo, 0m, 0, 1m, 0, 1m, 0m, 0m, []);

    /// <summary>Meses-calendário decorridos entre os dois meses, pela diferença entre eles.</summary>
    private static int Meses(DateOnly de, DateOnly ate) => Math.Max(0, (ate.Year - de.Year) * 12 + ate.Month - de.Month);

    private static decimal Fator(double baseMensal, int meses) => Math.Round((decimal)(1d / Math.Pow(baseMensal, meses)), 6);

    private static DateOnly Maior(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly Menor(DateOnly a, DateOnly b) => a < b ? a : b;
}
