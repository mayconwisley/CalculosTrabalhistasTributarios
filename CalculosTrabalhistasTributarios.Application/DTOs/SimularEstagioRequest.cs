namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Bolsa">Bolsa mensal do estagiário.</param>
/// <param name="AuxilioTransporte">Auxílio-transporte pago com a bolsa.</param>
/// <param name="Fim">Fim do estágio ou data até a qual o recesso é contado.</param>
/// <param name="DiasRecessoGozados">Dias de recesso já usufruídos no período.</param>
/// <param name="DiasNoMes">Dias de estágio na competência, no mês de início ou de fim; zero paga o mês inteiro.</param>
public sealed record SimularEstagioRequest(DateOnly Competencia, decimal Bolsa, decimal AuxilioTransporte, DateOnly Inicio, DateOnly Fim, decimal DiasRecessoGozados, int Dependentes, int DiasNoMes = 0);
