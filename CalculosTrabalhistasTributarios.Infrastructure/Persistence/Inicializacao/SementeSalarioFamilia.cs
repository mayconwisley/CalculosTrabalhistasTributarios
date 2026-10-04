namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence.Inicializacao;

/// <summary>Faixa do salário-família gravada na primeira execução.</summary>
internal sealed record SementeSalarioFamilia(int Ano, int Mes, int Dia, int Faixa, double LimiteRemuneracao, double Cota)
{
    public DateTime Competencia => new(Ano, Mes, Dia);
}
