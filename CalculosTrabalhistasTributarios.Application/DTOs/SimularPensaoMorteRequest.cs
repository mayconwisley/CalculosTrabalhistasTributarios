using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Domain.Previdencia.PensaoMorte;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Media">Média informada pronta do segurado não aposentado; ignorada quando os salários de contribuição são informados.</param>
/// <param name="Salarios">Salários de contribuição desde 07/1994 do segurado não aposentado, para calcular a média corrigida pelo INPC; nulo para usar a média informada.</param>
public sealed record SimularPensaoMorteRequest(
    DateOnly DataObito,
    SituacaoInstituidor Situacao,
    decimal ValorAposentadoria,
    decimal Media,
    SexoSegurado SexoInstituidor,
    int MesesContribuicao,
    CausaObito Causa,
    bool HaConjuge,
    int IdadeConjuge,
    bool UniaoDoisAnos,
    int OutrosDependentes,
    bool DependenteInvalido,
    decimal BeneficioConjuge,
    IReadOnlyList<PeriodoSalarioContribuicao>? Salarios = null);
