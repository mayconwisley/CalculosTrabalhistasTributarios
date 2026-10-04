namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence.Inicializacao;

/// <summary>Faixa de INSS gravada na primeira execução.</summary>
internal sealed record SementeFaixa(int Ano, int Mes, int Dia, int Faixa, double Limite, double Aliquota)
{
    public DateTime Competencia => new(Ano, Mes, Dia);
}
