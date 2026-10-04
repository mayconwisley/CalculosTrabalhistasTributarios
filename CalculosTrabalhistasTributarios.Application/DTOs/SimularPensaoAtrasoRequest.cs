using CalculosTrabalhistasTributarios.Domain.Judicial;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="DataAjuizamento">Data em que a execução foi proposta; sem ela, a separação dos ritos considera a data do cálculo.</param>
/// <param name="Acrescimos">Multa e honorários do art. 523 sobre o débito do rito da penhora.</param>
public sealed record SimularPensaoAtrasoRequest(IReadOnlyList<ParcelaPensaoInformada> Parcelas, DateOnly DataCalculo, DateOnly? DataAjuizamento, CorrecaoMonetaria Correcao, JurosDeMora Juros,
    AcrescimosPenhora Acrescimos = AcrescimosPenhora.Nenhum);
