using CalculosTrabalhistasTributarios.Domain.Previdencia;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="MesesNaReforma">Tempo de contribuição até 13/11/2019, em meses; zero para quem se filiou depois.</param>
/// <param name="Media">Média informada pronta; ignorada quando os salários de contribuição são informados.</param>
/// <param name="ExpectativaSobrevida">Expectativa de sobrevida da tábua do IBGE, para o fator previdenciário; nula se não informada.</param>
/// <param name="Salarios">Salários de contribuição desde 07/1994, para calcular a média corrigida pelo INPC; nulo para usar a média informada.</param>
public sealed record SimularAposentadoriaRequest(
    SexoSegurado Sexo,
    DateOnly Nascimento,
    DateOnly DataSimulacao,
    int MesesContribuicao,
    int MesesNaReforma,
    bool ContinuaContribuindo,
    decimal Media,
    decimal? ExpectativaSobrevida,
    IReadOnlyList<PeriodoSalarioContribuicao>? Salarios = null);
