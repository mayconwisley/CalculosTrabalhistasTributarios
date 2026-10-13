namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso;

/// <summary>O valor das horas de sobreaviso (1/3) e de prontidão (2/3) de uma competência, cada um arredondado nos centavos.</summary>
public sealed record SobreavisoDoMes(MesSobreaviso Mes, decimal ValorSobreaviso, decimal ValorProntidao)
{
    public decimal Total => ValorSobreaviso + ValorProntidao;
}
