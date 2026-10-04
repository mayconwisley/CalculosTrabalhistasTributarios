using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Pensao;

/// <summary>
/// Pensão alimentícia descontada de uma verba nas calculadoras de 13º, férias, rescisão e PLR. Na base líquida, a pensão
/// depende do IRRF e o IRRF depende da pensão, deduzida da base; o cálculo se repete até a pensão não mudar.
/// </summary>
public static class CalculadoraPensao
{
    /// <param name="verba">Valor sobre o qual a pensão incide.</param>
    /// <param name="inss">INSS da verba, abatido na base líquida.</param>
    /// <param name="apurar">Imposto da verba com a pensão informada deduzida da base.</param>
    public static Result<PensaoApurada<T>> Calcular<T>(RegraPensao regra, decimal verba, decimal inss, Func<decimal, T> apurar, Func<T, decimal> imposto)
    {
        if (regra.Validar() is { Falhou: true } invalida)
            return invalida.Erro;
        switch (regra.Base)
        {
            case BasePensao.SalarioMinimo:
                return Erro.Validacao("Nesta calculadora, a pensão é um percentual dos rendimentos ou um valor informado.");
            case BasePensao.ValorFixo:
                return new PensaoApurada<T>(regra.Valor, verba, apurar(regra.Valor));
            case BasePensao.RendimentosBrutos:
                var pensaoBruta = CalculadoraTributacao.Arredondar(verba * regra.Percentual / 100m);
                return new PensaoApurada<T>(pensaoBruta, verba, apurar(pensaoBruta));
        }

        var atual = 0m;
        var apuracao = apurar(atual);
        var basePensao = 0m;
        var pensao = 0m;
        for (var iteracao = 0; iteracao < 100; iteracao++)
        {
            basePensao = Math.Max(0m, verba - inss - imposto(apuracao));
            pensao = CalculadoraTributacao.Arredondar(basePensao * regra.Percentual / 100m);
            // Estável: o imposto foi apurado com esta mesma pensão deduzida.
            if (pensao == atual)
                break;
            atual = pensao;
            apuracao = apurar(atual);
        }
        return new PensaoApurada<T>(pensao, basePensao, apuracao);
    }
}
