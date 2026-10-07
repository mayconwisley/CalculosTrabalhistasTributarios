namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <param name="PeriodoApuracao">Competência do DAS, no primeiro dia do mês.</param>
/// <param name="ReceitaMes">Receita bruta do período de apuração, sobre a qual incide a alíquota efetiva.</param>
/// <param name="FolhaMes">Folha com encargos do período, usada no fator r do mês de início de atividade até 2026.</param>
/// <param name="InicioAtividade">Mês de início das atividades; nulo quando a empresa já tem mais de 13 meses.</param>
/// <param name="RemuneracoesMes">Salários e pró-labore do mês, base da CPP fora do DAS no Anexo IV.</param>
public sealed record EntradaSimples(
    DateOnly PeriodoApuracao,
    AtividadeSimples Atividade,
    decimal ReceitaMes,
    decimal FolhaMes,
    DateOnly? InicioAtividade,
    IReadOnlyList<MesSimples> Meses,
    decimal RemuneracoesMes);
