using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="BeneficiosClt">Benefícios pagos pela empresa ao CLT por mês, como vale-refeição e plano de saúde.</param>
/// <param name="RegimeEmpresa">Regime da empresa contratante, que define os encargos do CLT.</param>
/// <param name="ValorPj">Valor mensal da nota do PJ; zero calcula o valor que iguala o total do CLT.</param>
/// <param name="CustosPj">Custos mensais da empresa do PJ, como contabilidade e taxas.</param>
public sealed record SimularCltPjRequest(DateOnly Competencia, decimal SalarioClt, int Dependentes, decimal BeneficiosClt, RegimeTributario RegimeEmpresa, decimal ValorPj, TributacaoPj Tributacao, decimal CustosPj);
