using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// Alíquotas nominais e parcelas a deduzir dos Anexos I a V do Simples Nacional (LC 123/2006, com a redação da LC
/// 155/2016, em vigor desde 2018; Resolução CGSN 140/2018, Anexos I a V). Para 2027 e 2028, a Resolução CGSN 190/2026
/// reduz em 0,1 ponto a alíquota da 6ª faixa de todos os anexos; as demais faixas não mudam.
/// </summary>
public static class SimplesNacional
{
    public const decimal LimiteReceitaAnual = 4_800_000m;

    /// <summary>Sublimite acima do qual o ICMS e o ISS deixam de ser recolhidos no DAS (LC 123/2006, art. 13-A).</summary>
    public const decimal SublimiteIcmsIss = 3_600_000m;

    /// <summary>Folha de salários, inclusive o pró-labore, de pelo menos 28% da receita leva a atividade do anexo V para o III.</summary>
    public const decimal FatorRMinimo = 28m;

    private static readonly DateOnly InicioTabelas = new(2018, 1, 1);
    private static readonly DateOnly InicioTabelas2027 = new(2027, 1, 1);
    private static readonly DateOnly FimTabelas2027 = new(2028, 12, 1);
    private static readonly decimal[] Limites = [180_000m, 360_000m, 720_000m, 1_800_000m, 3_600_000m, 4_800_000m];

    // Alíquota nominal e parcela a deduzir das faixas 1 a 6; o último par é a 6ª faixa de 2027 e 2028.
    private static readonly Dictionary<AnexoSimples, (decimal Aliquota, decimal Deducao)[]> Tabelas = new()
    {
        [AnexoSimples.I] = [(4m, 0m), (7.3m, 5_940m), (9.5m, 13_860m), (10.7m, 22_500m), (14.3m, 87_300m), (19m, 378_000m), (18.9m, 378_000m)],
        [AnexoSimples.II] = [(4.5m, 0m), (7.8m, 5_940m), (10m, 13_860m), (11.2m, 22_500m), (14.7m, 85_500m), (30m, 720_000m), (29.9m, 720_000m)],
        [AnexoSimples.III] = [(6m, 0m), (11.2m, 9_360m), (13.5m, 17_640m), (16m, 35_640m), (21m, 125_640m), (33m, 648_000m), (32.9m, 648_000m)],
        [AnexoSimples.IV] = [(4.5m, 0m), (9m, 8_100m), (10.2m, 12_420m), (14m, 39_780m), (22m, 183_780m), (33m, 828_000m), (32.9m, 828_000m)],
        [AnexoSimples.V] = [(15.5m, 0m), (18m, 4_500m), (19.5m, 9_900m), (20.5m, 17_100m), (23m, 62_100m), (30.5m, 540_000m), (30.4m, 540_000m)]
    };

    /// <summary>A competência tem tabela cadastrada: de 01/2018 a 12/2028.</summary>
    public static bool TemTabela(DateOnly competencia) => competencia >= InicioTabelas && competencia <= FimTabelas2027;

    /// <param name="competencia">Período de apuração, que escolhe a tabela; sem ele, vale a tabela de 2018 a 2026.</param>
    public static Result<AliquotaSimples> Aliquota(AnexoSimples anexo, decimal receitaDozeMeses, DateOnly? competencia = null)
    {
        if (receitaDozeMeses > LimiteReceitaAnual)
            return Erro.Validacao("A receita de 12 meses passa do limite do Simples Nacional, de R$ 4,8 milhões por ano.");
        if (competencia is { } mes && !TemTabela(mes))
            return Erro.NaoEncontrado($"Não há tabela do Simples Nacional para {mes:MM/yyyy}: o cálculo cobre de 01/2018 a 12/2028.");

        var indice = Array.FindIndex(Limites, limite => receitaDozeMeses <= limite);
        var tabela = Tabelas[anexo];
        var (aliquota, deducao) = indice == 5 && competencia >= InicioTabelas2027 ? tabela[6] : tabela[indice];
        // Receita zero vale R$ 1,00 só para achar a alíquota (Resolução CGSN 140/2018, art. 21, parágrafo único): a nominal da 1ª faixa.
        var efetiva = receitaDozeMeses <= 0m ? aliquota : (receitaDozeMeses * aliquota / 100m - deducao) / receitaDozeMeses * 100m;
        return new AliquotaSimples(anexo, indice + 1, aliquota, deducao, efetiva);
    }

    /// <summary>Alíquota nominal da 1ª faixa, usada nos dois primeiros meses de atividade a partir de 2027.</summary>
    public static AliquotaSimples PrimeiraFaixa(AnexoSimples anexo)
    {
        var (aliquota, deducao) = Tabelas[anexo][0];
        return new AliquotaSimples(anexo, 1, aliquota, deducao, aliquota);
    }
}
