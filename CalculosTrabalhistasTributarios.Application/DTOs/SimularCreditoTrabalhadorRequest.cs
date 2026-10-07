using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record SimularCreditoTrabalhadorRequest(DateOnly Competencia, decimal RemuneracaoHabitual,
    decimal DescontosPrevidenciarios, int Dependentes, decimal Pensao, decimal OutrosDescontosCompulsorios,
    EntradaCreditoTrabalhador Credito);
