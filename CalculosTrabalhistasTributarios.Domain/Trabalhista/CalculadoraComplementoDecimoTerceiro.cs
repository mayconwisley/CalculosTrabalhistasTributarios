using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>
/// Revisão do 13º de quem recebe remuneração variável (Decreto 57.155/1965, art. 2º): em dezembro, a média usa as
/// variáveis até novembro; até 10 de janeiro, o cálculo é refeito com as variáveis de dezembro, e a diferença é paga ou
/// compensada. A média final divide as variáveis do ano pelos avos (12 no ano completo).
/// </summary>
public static class CalculadoraComplementoDecimoTerceiro
{
    private const decimal LimiteValor = 1_000_000_000m;

    public static Result<ApuracaoComplementoDecimoTerceiro> Calcular(decimal salario, decimal mediaPaga, decimal variaveisAteNovembro, decimal variaveisDezembro, int avos)
    {
        if (avos is < 1 or > 12)
            return Erro.Validacao("Os avos devem estar entre 1 e 12: um para cada mês com 15 dias ou mais de trabalho no ano.");
        decimal[] valores = [salario, mediaPaga, variaveisAteNovembro, variaveisDezembro];
        if (valores.Any(valor => valor < 0m || valor > LimiteValor || decimal.Round(valor, 2) != valor))
            return Erro.Validacao("Salário, média paga e variáveis devem ser positivos ou zero, até R$ 1 bilhão, com no máximo dois decimais.");
        if (variaveisAteNovembro + variaveisDezembro == 0m && mediaPaga == 0m)
            return Erro.Validacao("Informe a média usada na 2ª parcela ou as variáveis do ano: sem variáveis, não há complemento.");

        var variaveisAno = variaveisAteNovembro + variaveisDezembro;
        var mediaFinal = CalculadoraTributacao.Arredondar(variaveisAno / avos);
        return new ApuracaoComplementoDecimoTerceiro(salario, mediaPaga, variaveisAteNovembro, variaveisDezembro, variaveisAno, mediaFinal, avos,
            Integral(salario + mediaPaga, avos), Integral(salario + mediaFinal, avos));
    }

    private static decimal Integral(decimal remuneracao, int avos) => CalculadoraTributacao.Arredondar(remuneracao / 12m * avos);
}
