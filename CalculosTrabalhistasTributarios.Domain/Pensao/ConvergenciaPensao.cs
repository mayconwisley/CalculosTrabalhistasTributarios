using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Pensao;

public static class ConvergenciaPensao
{
    public const int MaximoIteracoes = 100;

    /// <summary>Executa a apuração com a pensão anterior e só aceita a pensão consistente com a própria apuração.</summary>
    public static Result<T> Calcular<T>(decimal inicial, Func<decimal, T> apurar,
        Func<T, decimal> obterPensao, bool exigeConvergencia = true)
    {
        var anterior = inicial;
        for (var iteracao = 0; iteracao < MaximoIteracoes; iteracao++)
        {
            var apuracao = apurar(anterior);
            var atual = obterPensao(apuracao);
            if (!exigeConvergencia || atual == anterior)
                return apuracao;
            anterior = atual;
        }
        return Validar(false).Erro;
    }

    public static Result Validar(bool estabilizou) => estabilizou ? Result.Ok()
        : Erro.Validacao("O cálculo da pensão e do IRRF não estabilizou. Revise as bases, os percentuais e a ordem dos beneficiários.");
}
