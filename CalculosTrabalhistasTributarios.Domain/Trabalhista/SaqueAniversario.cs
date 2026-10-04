using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>
/// Saque-aniversário do FGTS: todo ano, no mês do aniversário, uma parte do saldo pela tabela do anexo da Lei 8.036/1990
/// (art. 20-D): a alíquota da faixa sobre o saldo, mais a parcela adicional.
/// </summary>
public static class SaqueAniversario
{
    /// <summary>Limite de cada faixa, alíquota em % e parcela adicional; a última faixa não tem limite.</summary>
    private static readonly (decimal Limite, decimal Aliquota, decimal Adicional)[] Faixas =
    [
        (500m, 50m, 0m),
        (1_000m, 40m, 50m),
        (5_000m, 30m, 150m),
        (10_000m, 20m, 650m),
        (15_000m, 15m, 1_150m),
        (20_000m, 10m, 1_900m),
        (decimal.MaxValue, 5m, 2_900m)
    ];

    public static Result<ParcelaSaqueAniversario> Calcular(decimal saldo)
    {
        if (saldo <= 0m)
            return Erro.Validacao("Informe o saldo do FGTS, somando todas as contas.");
        var indice = Array.FindIndex(Faixas, faixa => saldo <= faixa.Limite);
        var (_, aliquota, adicional) = Faixas[indice];
        var valor = CalculadoraTributacao.Arredondar(saldo * aliquota / 100m + adicional);
        return new ParcelaSaqueAniversario(indice + 1, indice == 0 ? 0m : Faixas[indice - 1].Limite, aliquota, adicional, valor);
    }
}
