namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public sealed record ComissoesApuradas(
    decimal ValorInformado,
    decimal Comissoes,
    decimal Dsr,
    int DiasUteis,
    int DiasDescanso,
    int DescansosPerdidos,
    bool IncluiDsr,
    bool DiasInformados)
{
    public decimal Total => Comissoes + Dsr;
    public int DescansosPagos => DiasDescanso - DescansosPerdidos;
}
