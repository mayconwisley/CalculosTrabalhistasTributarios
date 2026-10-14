namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <summary>O reflexo da verba nas férias de um período aquisitivo.</summary>
/// <param name="Avos">Avos do período até a data de referência: 12 no período completo.</param>
/// <param name="Base">Soma das bases reflexas dos meses do período.</param>
/// <param name="Separado">Falso quando não há admissão: um único período com todos os meses, sem separar os aquisitivos.</param>
public sealed record FeriasReflexo(DateOnly Inicio, DateOnly Fim, int Avos, decimal Base, decimal Valor, decimal Terco, SituacaoFeriasReflexo Situacao, bool Separado = true)
{
    public bool Completo => Avos == 12;
    public bool Devidas => Situacao != SituacaoFeriasReflexo.NaoDevidas;
}
