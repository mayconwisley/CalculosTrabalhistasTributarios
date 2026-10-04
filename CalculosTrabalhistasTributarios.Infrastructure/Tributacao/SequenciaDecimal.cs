namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

internal static class SequenciaDecimal
{
    /// <summary>Cinco faixas, a primeira isenta, com limites, alíquotas e deduções crescentes e a última "acima de".</summary>
    public static void ValidarFaixasProgressivas(IReadOnlyList<FaixaIrrfPublicada> faixas, string nomeTabela)
    {
        if (faixas.Count != 5 ||
            faixas[0].Limite <= 0m ||
            faixas[0].Aliquota != 0m ||
            faixas[0].Deducao != 0m ||
            faixas[^1].Limite != TabelaIrrfPublicada.LimiteUltimaFaixa ||
            !faixas.Select(faixa => faixa.Limite).EstritamenteCrescente() ||
            !faixas.Select(faixa => faixa.Aliquota).EstritamenteCrescente() ||
            !faixas.Select(faixa => faixa.Deducao).EstritamenteCrescente())
            throw new InvalidOperationException($"A página retornou uma estrutura de faixas {nomeTabela} inválida.");
    }

    public static bool EstritamenteCrescente(this IEnumerable<decimal> valores)
    {
        decimal? anterior = null;
        foreach (var valor in valores)
        {
            if (valor <= anterior) return false;
            anterior = valor;
        }
        return true;
    }
}
