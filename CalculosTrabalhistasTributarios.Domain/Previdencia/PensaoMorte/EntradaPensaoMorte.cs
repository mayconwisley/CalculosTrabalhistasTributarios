namespace CalculosTrabalhistasTributarios.Domain.Previdencia.PensaoMorte;

/// <summary>O segurado que faleceu, os dependentes e os parâmetros da data do óbito.</summary>
/// <param name="ValorAposentadoria">Aposentadoria que o segurado recebia; usada quando ele era aposentado.</param>
/// <param name="Media">Média dos salários de contribuição, base da aposentadoria por incapacidade de quem não era aposentado.</param>
/// <param name="MesesContribuicao">Tempo de contribuição do segurado na data do óbito, em meses.</param>
/// <param name="IdadeConjuge">Idade do cônjuge ou companheiro na data do óbito, em anos completos.</param>
/// <param name="UniaoDoisAnos">Casamento ou união estável com pelo menos 2 anos na data do óbito.</param>
/// <param name="OutrosDependentes">Filhos e demais dependentes além do cônjuge.</param>
/// <param name="DependenteInvalido">Há dependente inválido ou com deficiência intelectual, mental ou grave: a pensão é de 100%.</param>
/// <param name="BeneficioConjuge">Aposentadoria ou pensão do próprio cônjuge, para o redutor da acumulação; zero se não houver.</param>
public sealed record EntradaPensaoMorte(
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
    decimal SalarioMinimo,
    decimal Teto);
