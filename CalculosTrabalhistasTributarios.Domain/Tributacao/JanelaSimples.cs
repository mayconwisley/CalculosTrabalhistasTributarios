namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Competências que formam a receita e a folha de 12 meses do período de apuração, e a regra aplicada.</summary>
/// <param name="MesDeAtividade">1 no mês de início; nulo sem data de início informada.</param>
/// <param name="Regra2027">Verdadeiro a partir de 01/2027, quando a janela termina no mês anterior ao anterior do período (Resolução CGSN 190/2026).</param>
public sealed record JanelaSimples(IReadOnlyList<DateOnly> Meses, RegraReceitaSimples Regra, int? MesDeAtividade, bool Regra2027);
