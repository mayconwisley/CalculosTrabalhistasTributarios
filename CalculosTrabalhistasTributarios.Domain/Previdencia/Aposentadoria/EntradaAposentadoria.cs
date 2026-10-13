namespace CalculosTrabalhistasTributarios.Domain.Previdencia.Aposentadoria;

/// <summary>Os dados do segurado na data da simulação e as premissas da projeção.</summary>
/// <param name="MesesContribuicao">Tempo de contribuição até a data da simulação, em meses.</param>
/// <param name="MesesNaReforma">Tempo de contribuição até 13/11/2019, em meses; zero para quem se filiou depois.</param>
/// <param name="ContinuaContribuindo">Se o tempo de contribuição cresce um mês a cada mês da projeção.</param>
/// <param name="Media">Média de todos os salários de contribuição desde 07/1994, corrigidos, em valores de hoje.</param>
/// <param name="ExpectativaSobrevida">Expectativa de sobrevida na idade da aposentadoria (tábua do IBGE), para o fator previdenciário; nula quando não informada.</param>
public sealed record EntradaAposentadoria(
    SexoSegurado Sexo,
    DateOnly Nascimento,
    DateOnly DataSimulacao,
    int MesesContribuicao,
    int MesesNaReforma,
    bool ContinuaContribuindo,
    decimal Media,
    decimal SalarioMinimo,
    decimal Teto,
    decimal? ExpectativaSobrevida = null);
