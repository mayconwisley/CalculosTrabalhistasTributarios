namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <param name="Valor">Comissões sem DSR ou total já pago com DSR, conforme <paramref name="IncluiDsr"/>.</param>
/// <param name="Feriados">Feriados que não coincidem com domingos; usados apenas na contagem automática.</param>
/// <param name="DiasUteis">Dias úteis do período; informar junto com <paramref name="DiasDescanso"/> para substituir o calendário.</param>
/// <param name="DiasDescanso">Dias de repouso previstos no período, antes dos repousos perdidos.</param>
public sealed record ComissoesInformadas(
    DateOnly Competencia,
    decimal Valor,
    bool IncluiDsr,
    int Feriados,
    int DescansosPerdidos,
    int? DiasUteis = null,
    int? DiasDescanso = null);
