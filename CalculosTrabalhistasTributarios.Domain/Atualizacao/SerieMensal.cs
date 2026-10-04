using CalculosTrabalhistasTributarios.Domain.Comum;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Domain.Atualizacao;

/// <summary>Acumula os valores mensais de um índice ou de uma taxa, em %, num intervalo de meses; os dois extremos entram.</summary>
public static class SerieMensal
{
    /// <summary>Variações compostas mês a mês, como na correção monetária: (1 + v1) x (1 + v2) x ...; sem meses, 1.</summary>
    public static Result<decimal> FatorComposto(IReadOnlyDictionary<DateOnly, decimal> serie, DateOnly primeiroMes, DateOnly ultimoMes, string nome)
    {
        var fator = 1m;
        foreach (var mes in Meses(primeiroMes, ultimoMes))
        {
            if (!serie.TryGetValue(mes, out var valor))
                return MesAusente(nome, mes);
            fator *= 1m + valor / 100m;
        }
        return fator;
    }

    /// <summary>Taxas somadas mês a mês, em juros simples, como a Selic dos tributos em atraso; sem meses, zero.</summary>
    public static Result<decimal> SomaSimples(IReadOnlyDictionary<DateOnly, decimal> serie, DateOnly primeiroMes, DateOnly ultimoMes, string nome)
    {
        var soma = 0m;
        foreach (var mes in Meses(primeiroMes, ultimoMes))
        {
            if (!serie.TryGetValue(mes, out var valor))
                return MesAusente(nome, mes);
            soma += valor;
        }
        return soma;
    }

    /// <summary>Quantidade de meses de <paramref name="primeiroMes"/> a <paramref name="ultimoMes"/>, contando os dois.</summary>
    public static int Quantidade(DateOnly primeiroMes, DateOnly ultimoMes) =>
        Math.Max(0, (ultimoMes.Year - primeiroMes.Year) * 12 + ultimoMes.Month - primeiroMes.Month + 1);

    public static DateOnly MesDe(DateOnly data) => new(data.Year, data.Month, 1);

    private static IEnumerable<DateOnly> Meses(DateOnly primeiroMes, DateOnly ultimoMes)
    {
        for (var mes = MesDe(primeiroMes); mes <= MesDe(ultimoMes); mes = mes.AddMonths(1))
            yield return mes;
    }

    private static Erro MesAusente(string nome, DateOnly mes) =>
        Erro.NaoEncontrado($"Falta o valor de {nome} de {mes.ToString("MM/yyyy", CultureInfo.InvariantCulture)} na tabela de índices. Atualize a tabela {nome} pela internet ou cadastre o mês.");
}
