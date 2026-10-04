namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Retenção mensal de 10% sobre lucros e dividendos (Lei 9.250/1995, art. 6º-A, e Lei 9.249/1995, art. 10, § 4º).</summary>
/// <param name="Valor">Total pago no mês pela mesma empresa à mesma pessoa.</param>
/// <param name="ParteTransicao">Parte de lucros apurados até 2025, com distribuição aprovada até 31/12/2025, que não sofre retenção.</param>
/// <param name="JaRetido">IRRF já retido em pagamentos anteriores do mesmo mês.</param>
/// <param name="Exterior">Beneficiário residente no exterior: 10% sobre qualquer valor.</param>
public sealed record SimularDividendosRequest(DateOnly Competencia, decimal Valor, decimal ParteTransicao, decimal JaRetido, bool Exterior);
