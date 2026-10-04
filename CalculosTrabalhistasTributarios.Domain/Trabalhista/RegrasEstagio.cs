namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>Estágio (Lei 11.788/2008): recesso de 30 dias por ano, proporcional ao tempo, e duração máxima de 2 anos.</summary>
public static class RegrasEstagio
{
    public const int DiasDeRecessoPorAno = 30;

    /// <summary>O estágio na mesma parte concedente não passa de 2 anos, exceto para a pessoa com deficiência (art. 11).</summary>
    public const int MesesMaximos = 24;

    /// <summary>Recesso pelos meses de estágio: 30 dias a cada 12 meses e a proporção do restante (art. 13).</summary>
    public static decimal DiasDeRecesso(int meses) => Math.Round(DiasDeRecessoPorAno * meses / 12m, 1);
}
