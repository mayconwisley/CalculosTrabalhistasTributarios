namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence.Inicializacao;

/// <summary>Valor mensal (dedução, desconto, salário mínimo ou índice) gravado na primeira execução.</summary>
internal sealed record SementeParametro(int Ano, int Mes, int Dia, double Valor)
{
    public DateTime Competencia => new(Ano, Mes, Dia);
}
