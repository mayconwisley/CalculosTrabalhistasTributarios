using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Pensao;

/// <summary>Como a decisão judicial ou o acordo define a pensão alimentícia: um percentual de uma base ou um valor fixo.</summary>
/// <param name="Percentual">De 0 a 100 sobre os rendimentos; até 1.000 sobre o salário mínimo (150 = 1,5 salário mínimo).</param>
/// <param name="Valor">Valor da pensão; usado com <see cref="BasePensao.ValorFixo"/>.</param>
public sealed record RegraPensao(BasePensao Base, decimal Percentual, decimal Valor)
{
    public static RegraPensao PercentualDosLiquidos(decimal percentual) => new(BasePensao.RendimentosLiquidos, percentual, 0m);

    public bool EhPercentual => Base != BasePensao.ValorFixo;

    /// <summary>Sobre os rendimentos, até 100%; sobre o salário mínimo, a pensão pode passar de um salário (150% = 1,5 salário).</summary>
    public decimal PercentualMaximo => Base == BasePensao.SalarioMinimo ? 1000m : 100m;

    /// <summary>Falha com o que corrigir quando o percentual ou o valor está fora dos limites.</summary>
    public Result Validar()
    {
        if (EhPercentual && (Percentual < 0m || Percentual > PercentualMaximo))
            return Erro.Validacao(Base == BasePensao.SalarioMinimo
                ? "O percentual do salário mínimo deve estar entre 0 e 1.000 (150 para 1,5 salário mínimo)."
                : "O percentual da pensão deve estar entre 0 e 100.");
        if (!EhPercentual && Valor < 0m)
            return Erro.Validacao("O valor da pensão não pode ser negativo.");
        return Result.Ok();
    }

    /// <summary>A regra em uma frase, como "30,00% dos rendimentos líquidos".</summary>
    public string Descrever(Func<decimal, string> moeda, Func<decimal, string> percentual) => Base switch
    {
        BasePensao.RendimentosLiquidos => $"{percentual(Percentual)} dos rendimentos líquidos",
        BasePensao.RendimentosBrutos => $"{percentual(Percentual)} dos rendimentos brutos",
        BasePensao.SalarioMinimo => $"{percentual(Percentual)} do salário mínimo",
        _ => $"Valor fixo de {moeda(Valor)}"
    };
}
