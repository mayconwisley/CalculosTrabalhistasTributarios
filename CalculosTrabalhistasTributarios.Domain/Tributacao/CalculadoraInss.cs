namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// Apura o INSS conforme o regime aplicável à competência. O valor de cada faixa é truncado nos centavos, como o eSocial
/// calcula a contribuição (Manual de Orientação do eSocial, S-5001), em vez de arredondado.
/// </summary>
public static class CalculadoraInss
{
    private static readonly DateOnly InicioRegimeProgressivo = new(2020, 3, 1);

    /// <summary>Desde 03/2020, cada parte da base paga a alíquota da sua faixa (EC 103/2019, art. 28); antes, a alíquota da faixa incidia sobre toda a base.</summary>
    public static bool Progressivo(DateOnly competencia) => competencia >= InicioRegimeProgressivo;

    public static IReadOnlyList<ResultadoFaixaTributaria> CalcularDetalhes(
        DateOnly competencia,
        decimal baseCalculo,
        IReadOnlyList<FaixaTributaria> faixas)
    {
        if (Progressivo(competencia))
            return CalculadoraTributacao.CalcularProgressivo(baseCalculo, faixas, CalculadoraTributacao.Truncar);

        if (baseCalculo <= 0m)
            return [];

        var faixa = faixas.OrderBy(item => item.Numero).FirstOrDefault(item => baseCalculo <= item.Limite) ?? faixas.MaxBy(item => item.Limite)
            ?? throw new InvalidOperationException("Não há faixas de INSS cadastradas para a competência informada.");
        var contribuicao = CalculadoraTributacao.Truncar(baseCalculo * faixa.AliquotaDecimal);

        return [new ResultadoFaixaTributaria(faixa.Numero, baseCalculo, faixa.Aliquota, contribuicao)];
    }

    /// <summary>Calcula apenas a fatia entre duas bases acumuladas, truncando cada faixa como no eSocial.</summary>
    internal static IReadOnlyList<ResultadoFaixaTributaria> CalcularDetalhesIntervalo(
        decimal baseAnterior, decimal baseFinal, IReadOnlyList<FaixaTributaria> faixas)
    {
        if (baseAnterior < 0m || baseFinal < baseAnterior)
            throw new ArgumentOutOfRangeException(nameof(baseFinal));
        if (baseFinal == baseAnterior)
            return [];

        var resultado = new List<ResultadoFaixaTributaria>();
        var limiteAnterior = 0m;
        foreach (var faixa in faixas.OrderBy(item => item.Numero))
        {
            var baseDaFaixa = Math.Min(baseFinal, faixa.Limite) - Math.Max(baseAnterior, limiteAnterior);
            if (baseDaFaixa > 0m)
                resultado.Add(new ResultadoFaixaTributaria(faixa.Numero, baseDaFaixa, faixa.Aliquota,
                    CalculadoraTributacao.Truncar(baseDaFaixa * faixa.AliquotaDecimal)));
            if (baseFinal <= faixa.Limite)
                break;
            limiteAnterior = faixa.Limite;
        }
        return resultado;
    }
}
