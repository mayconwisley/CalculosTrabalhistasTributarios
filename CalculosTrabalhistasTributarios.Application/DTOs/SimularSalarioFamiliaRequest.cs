namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Remuneracao">Remuneração do mês, comparada ao limite do salário-família.</param>
/// <param name="Filhos">Filhos ou equiparados de até 14 anos, ou inválidos de qualquer idade.</param>
/// <param name="DiasTrabalhados">Dias do mês; nos meses de admissão e desligamento a cota é proporcional.</param>
public sealed record SimularSalarioFamiliaRequest(DateOnly Competencia, decimal Remuneracao, int Filhos, int DiasTrabalhados);
