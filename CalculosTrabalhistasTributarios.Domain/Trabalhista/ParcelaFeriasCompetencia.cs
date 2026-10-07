namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public sealed record ParcelaFeriasCompetencia(DateOnly Competencia, int Dias, decimal Ferias, decimal Terco)
{
    public decimal BaseInss => Ferias + Terco;
}
