namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Bolsa">Bolsa mensal do estagiário.</param>
/// <param name="AuxilioTransporte">Auxílio-transporte pago com a bolsa.</param>
/// <param name="Fim">Fim do estágio ou data até a qual o recesso é contado.</param>
/// <param name="DiasRecessoGozados">Dias de recesso já usufruídos no período.</param>
public sealed record SimularEstagioRequest(DateOnly Competencia, decimal Bolsa, decimal AuxilioTransporte, DateOnly Inicio, DateOnly Fim, decimal DiasRecessoGozados, int Dependentes);
