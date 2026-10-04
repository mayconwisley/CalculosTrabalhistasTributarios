namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Adicionais">Horas extras, adicional noturno e outros proventos do mês, com INSS, IRRF e FGTS.</param>
/// <param name="Faltas">Faltas injustificadas no mês, descontadas a um trinta avos do salário cada.</param>
/// <param name="CustoValeTransporte">Custo mensal das passagens; o desconto é de até 6% do salário (Lei 7.418/1985, art. 4º).</param>
public sealed record SimularDomesticoRequest(DateOnly Competencia, decimal Salario, decimal Adicionais, int Faltas, int Dependentes, decimal CustoValeTransporte);
