namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <param name="DsrRepercute">O DSR do mês entra na base do 13º, das férias e do aviso.</param>
/// <param name="BaseReflexa">Valor do mês e, quando repercute, o DSR: a base do 13º, das férias e do aviso.</param>
public sealed record ReflexoDoMes(DateOnly Competencia, decimal Valor, int DiasUteis, int DiasDescanso, decimal Dsr, bool DsrRepercute, decimal BaseReflexa);
