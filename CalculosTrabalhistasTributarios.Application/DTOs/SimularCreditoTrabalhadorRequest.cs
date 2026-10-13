using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="ParcelasPagasParaQuitacao">Parcelas já pagas para simular a quitação antecipada; zero quando não se simula.</param>
public sealed record SimularCreditoTrabalhadorRequest(DateOnly Competencia, decimal RemuneracaoHabitual,
    decimal DescontosPrevidenciarios, int Dependentes, decimal Pensao, decimal OutrosDescontosCompulsorios,
    EntradaCreditoTrabalhador Credito, int ParcelasPagasParaQuitacao = 0);
