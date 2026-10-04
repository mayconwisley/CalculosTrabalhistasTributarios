namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence.Inicializacao;

/// <summary>Faixa com dedução (IRRF, PLR e seguro-desemprego) gravada na primeira execução.</summary>
internal sealed record SementeIrrf(int Ano, int Mes, int Dia, int Faixa, double Limite, double Aliquota, double Deducao)
{
    public DateTime Competencia => new(Ano, Mes, Dia);
}
