using CalculosTrabalhistasTributarios.Domain.Comum;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Conferência dos <see cref="Result"/>: o valor de um cálculo que deve dar certo, ou o erro de um que deve falhar.</summary>
internal static class Resultados
{
    public static T Sucesso<T>(this Result<T> resultado)
    {
        Assert.True(resultado.Sucesso, resultado.Falhou ? $"Esperava sucesso, mas falhou: {resultado.Erro.Mensagem}" : null);
        return resultado.Valor;
    }

    public static async Task<T> Sucesso<T>(this Task<Result<T>> resultado) => (await resultado).Sucesso();

    public static Erro Falha(this Result resultado)
    {
        Assert.True(resultado.Falhou, "Esperava uma falha, mas o resultado deu certo.");
        return resultado.Erro;
    }

    public static async Task<Erro> Falha<T>(this Task<Result<T>> resultado) => (await resultado).Falha();
}
