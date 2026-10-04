namespace CalculosTrabalhistasTributarios.Tests.Referencia;

/// <summary>
/// Cálculo de referência do INSS e do IRRF mensal, escrito à parte do aplicativo e com as tabelas digitadas das fontes
/// oficiais (gov.br/INSS, Receita Federal e Lei 15.270/2025). Os testes comparam o aplicativo com este modelo: se uma
/// tabela do banco ou uma regra do cálculo mudar sem querer, eles falham.
/// </summary>
internal static class ModeloTributario
{
    private const decimal Dependente = 189.59m;
    private const decimal LimiteDispensa = 10m;

    private sealed record TabelaInss(bool Unica, (decimal Limite, decimal Aliquota)[] Faixas);
    private sealed record FaixaIrrf(decimal? Limite, decimal Aliquota, decimal Deducao);

    private static readonly FaixaIrrf[] Irrf2017 = [new(1903.98m, 0m, 0m), new(2826.65m, 7.5m, 142.80m), new(3751.05m, 15m, 354.80m), new(4664.68m, 22.5m, 636.13m), new(null, 27.5m, 869.36m)];
    private static readonly FaixaIrrf[] Irrf2023 = [new(2112m, 0m, 0m), new(2826.65m, 7.5m, 158.40m), new(3751.05m, 15m, 370.40m), new(4664.68m, 22.5m, 651.73m), new(null, 27.5m, 884.96m)];
    private static readonly FaixaIrrf[] Irrf2024 = [new(2259.20m, 0m, 0m), new(2826.65m, 7.5m, 169.44m), new(3751.05m, 15m, 381.44m), new(4664.68m, 22.5m, 662.77m), new(null, 27.5m, 896.00m)];
    private static readonly FaixaIrrf[] Irrf2025 = [new(2428.80m, 0m, 0m), new(2826.65m, 7.5m, 182.16m), new(3751.05m, 15m, 394.16m), new(4664.68m, 22.5m, 675.49m), new(null, 27.5m, 908.73m)];

    /// <summary>Competências cobertas pelo modelo: as tabelas de cada uma, como publicadas.</summary>
    private static readonly Dictionary<DateOnly, (TabelaInss Inss, FaixaIrrf[] Irrf, decimal? Simplificado, bool Reducao2026)> Tabelas = new()
    {
        [new(2019, 6, 1)] = (new(true, [(1751.81m, 8m), (2919.72m, 9m), (5839.45m, 11m)]), Irrf2017, null, false),
        [new(2020, 1, 1)] = (new(true, [(1830.29m, 8m), (3050.52m, 9m), (6101.06m, 11m)]), Irrf2017, null, false),
        [new(2021, 6, 1)] = (new(false, [(1100m, 7.5m), (2203.48m, 9m), (3305.22m, 12m), (6433.57m, 14m)]), Irrf2017, null, false),
        [new(2023, 4, 1)] = (new(false, [(1302m, 7.5m), (2571.29m, 9m), (3856.94m, 12m), (7507.49m, 14m)]), Irrf2017, null, false),
        [new(2023, 5, 1)] = (new(false, [(1320m, 7.5m), (2571.29m, 9m), (3856.94m, 12m), (7507.49m, 14m)]), Irrf2023, 528m, false),
        [new(2024, 6, 1)] = (new(false, [(1412m, 7.5m), (2666.68m, 9m), (4000.03m, 12m), (7786.02m, 14m)]), Irrf2024, 564.80m, false),
        [new(2025, 6, 1)] = (new(false, [(1518m, 7.5m), (2793.88m, 9m), (4190.83m, 12m), (8157.41m, 14m)]), Irrf2025, 607.20m, false),
        [new(2026, 10, 1)] = (new(false, [(1621m, 7.5m), (2902.84m, 9m), (4354.27m, 12m), (8475.55m, 14m)]), Irrf2025, 607.20m, true)
    };

    public static IEnumerable<DateOnly> Competencias => Tabelas.Keys;

    public sealed record Resultado(decimal Inss, decimal ImpostoNormal, decimal? ImpostoSimplificado, decimal IrrfRetido, decimal Liquido);

    public static Resultado Calcular(DateOnly competencia, decimal bruto, int dependentes)
    {
        var (inssTabela, irrf, simplificado, reducao) = Tabelas[competencia];
        var inss = Inss(inssTabela, bruto);
        var normal = Modalidade(irrf, reducao, bruto, Math.Max(0m, bruto - inss - dependentes * Dependente));
        decimal? simples = simplificado is { } desconto ? Modalidade(irrf, reducao, bruto, Math.Max(0m, bruto - desconto)) : null;
        var calculado = simples is { } s ? Math.Min(normal, s) : normal;
        var retido = calculado is > 0m and <= LimiteDispensa ? 0m : calculado;
        return new(inss, normal, simples, retido, bruto - inss - retido);
    }

    /// <summary>INSS com o valor de cada faixa truncado nos centavos, como o eSocial (evento S-5001).</summary>
    private static decimal Inss(TabelaInss tabela, decimal bruto)
    {
        var teto = tabela.Faixas[^1].Limite;
        var baseInss = Math.Min(bruto, teto);
        if (baseInss <= 0m)
            return 0m;
        if (tabela.Unica)
        {
            var aliquota = tabela.Faixas.First(faixa => baseInss <= faixa.Limite).Aliquota;
            return Truncar(baseInss * aliquota / 100m);
        }
        var total = 0m;
        var anterior = 0m;
        foreach (var (limite, aliquota) in tabela.Faixas)
        {
            if (baseInss > anterior)
                total += Truncar((Math.Min(baseInss, limite) - anterior) * aliquota / 100m);
            anterior = limite;
        }
        return total;
    }

    private static decimal Modalidade(FaixaIrrf[] faixas, bool reducao2026, decimal rendimentos, decimal baseIrrf)
    {
        var faixa = faixas.First(item => item.Limite is null || baseIrrf <= item.Limite);
        var imposto = Arredondar(baseIrrf * faixa.Aliquota / 100m - faixa.Deducao);
        if (!reducao2026 || imposto <= 0m || rendimentos > 7350m)
            return imposto;
        // Lei 15.270/2025: até R$ 5.000,00 de rendimentos, redução de até R$ 312,89; até R$ 7.350,00, 978,62 − 0,133145 × rendimentos.
        var valorReducao = rendimentos <= 5000m ? 312.89m : 978.62m - 0.133145m * rendimentos;
        return Arredondar(imposto - Math.Min(imposto, Math.Max(0m, Arredondar(valorReducao))));
    }

    public static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    public static decimal Truncar(decimal valor) => Math.Round(valor, 2, MidpointRounding.ToZero);
}
