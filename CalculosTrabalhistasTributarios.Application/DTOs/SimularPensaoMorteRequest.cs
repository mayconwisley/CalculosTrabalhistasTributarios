using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Domain.Previdencia.PensaoMorte;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

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
    decimal BeneficioConjuge);
