using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// Alíquotas dos anexos III e V do Simples Nacional (Lei Complementar 123/2006, com a redação da LC 155/2016, em vigor desde
/// 2018). A alíquota efetiva depende da receita bruta dos últimos 12 meses.
/// </summary>
public static class SimplesNacional
{
    public const decimal LimiteReceitaAnual = 4_800_000m;

    /// <summary>Folha de salários, inclusive o pró-labore, de pelo menos 28% da receita leva a atividade do anexo V para o III.</summary>
    public const decimal FatorRMinimo = 28m;

    private static readonly (decimal Limite, decimal Aliquota, decimal Deducao)[] AnexoIII =
    [
        (180_000m, 6m, 0m), (360_000m, 11.2m, 9_360m), (720_000m, 13.5m, 17_640m),
        (1_800_000m, 16m, 35_640m), (3_600_000m, 21m, 125_640m), (4_800_000m, 33m, 648_000m)
    ];

    private static readonly (decimal Limite, decimal Aliquota, decimal Deducao)[] AnexoV =
    [
        (180_000m, 15.5m, 0m), (360_000m, 18m, 4_500m), (720_000m, 19.5m, 9_900m),
        (1_800_000m, 20.5m, 17_100m), (3_600_000m, 23m, 62_100m), (4_800_000m, 30.5m, 540_000m)
    ];

    public static Result<AliquotaSimples> Aliquota(AnexoSimples anexo, decimal receitaDozeMeses)
    {
        if (receitaDozeMeses > LimiteReceitaAnual)
            return Erro.Validacao("A receita de 12 meses passa do limite do Simples Nacional, de R$ 4,8 milhões por ano.");

        var faixas = anexo == AnexoSimples.III ? AnexoIII : AnexoV;
        var indice = Array.FindIndex(faixas, faixa => receitaDozeMeses <= faixa.Limite);
        var (_, aliquota, deducao) = faixas[indice];
        var efetiva = receitaDozeMeses <= 0m ? aliquota : (receitaDozeMeses * aliquota / 100m - deducao) / receitaDozeMeses * 100m;
        return new AliquotaSimples(anexo, indice + 1, aliquota, deducao, efetiva);
    }
}
