namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Comissoes">Valor sem DSR, salvo quando <paramref name="IncluiDsr"/> for verdadeiro.</param>
/// <param name="DiasUteis">Nulo para usar o calendário da competência.</param>
/// <param name="DiasDescanso">Nulo para usar domingos e os feriados informados.</param>
public sealed record SimularComissoesRequest(
    DateOnly Competencia,
    decimal SalarioFixo,
    decimal Comissoes,
    bool IncluiDsr,
    int Feriados,
    int DescansosPerdidos,
    int Dependentes,
    int? DiasUteis = null,
    int? DiasDescanso = null,
    decimal PisoGarantido = 0m);
