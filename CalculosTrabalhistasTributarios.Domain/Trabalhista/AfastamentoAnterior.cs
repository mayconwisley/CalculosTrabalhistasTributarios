namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>
/// Afastamento anterior pelo mesmo motivo, que pode reduzir os 15 dias a cargo da empresa no novo afastamento (Decreto
/// 3.048/1999, art. 75, §§ 3º a 5º). Que o motivo é o mesmo é uma premissa informada, e não verificada pelo cálculo.
/// </summary>
/// <param name="Fim">Último dia do afastamento anterior ou do benefício anterior.</param>
/// <param name="DiasPagosPelaEmpresa">Dias do afastamento anterior pagos pela empresa, de 1 a 15; ignorado no benefício do INSS.</param>
public sealed record AfastamentoAnterior(OrigemAfastamentoAnterior Origem, DateOnly Fim, int DiasPagosPelaEmpresa);
