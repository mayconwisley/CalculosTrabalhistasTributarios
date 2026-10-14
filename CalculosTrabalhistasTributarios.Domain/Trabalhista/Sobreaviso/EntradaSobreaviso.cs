using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso;

/// <summary>Os meses de sobreaviso e prontidão e as premissas dos reflexos, informadas por quem simula.</summary>
/// <param name="Divisor">Divisor do salário-hora, como 220 para a jornada de 44 horas semanais.</param>
/// <param name="Habitual">Horas habituais refletem no DSR, no 13º, nas férias e no aviso; as eventuais têm só o FGTS.</param>
/// <param name="DsrRepercuteNasOutras">Se o DSR sobre as horas entra na base do 13º, das férias e do aviso.</param>
/// <param name="FeriasGozadas">Verdadeiro no contrato ativo; falso na dispensa, quando as férias não gozadas são indenizadas, sem FGTS.</param>
/// <param name="Admissao">Data de admissão, que separa os períodos aquisitivos das férias; nula para somar todos os meses.</param>
public sealed record EntradaSobreaviso(
    IReadOnlyList<MesSobreaviso> Meses,
    decimal Divisor,
    bool Habitual,
    IncidenciasReflexo Incidencias,
    bool DsrRepercuteNasOutras = false,
    int DiasAvisoPrevio = 0,
    bool FeriasGozadas = true,
    DateOnly? Admissao = null);
