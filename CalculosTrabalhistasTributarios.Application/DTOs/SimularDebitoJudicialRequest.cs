using CalculosTrabalhistasTributarios.Domain.Judicial;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="DataAjuizamento">Débito trabalhista: início da fase judicial; sem ela, todo o período é pré-judicial.</param>
/// <param name="DataCitacao">Débito cível com juros desde a citação.</param>
/// <param name="Acrescimos">Débito cível: multa e honorários do cumprimento de sentença (CPC, art. 523, § 1º).</param>
public sealed record SimularDebitoJudicialRequest(
    NaturezaDebito Natureza,
    IReadOnlyList<ParcelaDebito> Parcelas,
    DateOnly DataCalculo,
    DateOnly? DataAjuizamento,
    InicioJurosCivel InicioJuros,
    DateOnly? DataCitacao,
    IndiceCivelAnterior IndiceAnterior,
    AcrescimosPenhora Acrescimos = AcrescimosPenhora.Nenhum);
