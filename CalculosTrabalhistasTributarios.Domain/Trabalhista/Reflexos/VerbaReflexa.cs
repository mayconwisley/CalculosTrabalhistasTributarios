namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <summary>Uma verba mês a mês e as premissas dos seus reflexos.</summary>
/// <param name="Habitual">Verba paga com habitualidade: só ela reflete no DSR, no 13º, nas férias e no aviso prévio.</param>
/// <param name="DsrRepercuteNasOutras">Nas verbas que não são horas extras, se o DSR delas entra na base do 13º, das férias e do aviso.</param>
/// <param name="DiasAvisoPrevio">Dias do aviso prévio indenizado, de 30 a 90 (Lei 12.506/2011); zero quando não há.</param>
/// <param name="FeriasGozadas">
/// Verdadeiro no contrato ativo: as férias dos períodos completos são gozadas, com FGTS. Falso na dispensa: as férias
/// ainda não gozadas são indenizadas na rescisão, sem FGTS (OJ 195 da SBDI-1 do TST).
/// </param>
/// <param name="Admissao">Data de admissão, que separa os períodos aquisitivos das férias; nula para somar todos os meses num só período.</param>
public sealed record VerbaReflexa(
    string Descricao,
    NaturezaVerbaReflexa Natureza,
    bool Habitual,
    IncidenciasReflexo Incidencias,
    IReadOnlyList<MesVerbaReflexa> Meses,
    bool DsrRepercuteNasOutras = false,
    int DiasAvisoPrevio = 0,
    bool FeriasGozadas = true,
    DateOnly? Admissao = null);
