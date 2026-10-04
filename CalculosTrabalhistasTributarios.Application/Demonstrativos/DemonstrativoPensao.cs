using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>Memória de cálculo e referência da pensão alimentícia descontada de uma verba.</summary>
internal static class DemonstrativoPensao
{
    /// <param name="rotuloVerba">Como a verba aparece na fórmula, por exemplo "13º integral".</param>
    /// <param name="inss">INSS abatido na base líquida; nulo quando a verba não tem INSS, como a PLR.</param>
    /// <param name="deducaoNoImposto">Como a pensão reduz o imposto da verba.</param>
    public static GrupoMemoriaDto Memoria(string titulo, RegraPensao regra, decimal verba, string rotuloVerba, decimal? inss, decimal imposto, decimal basePensao, decimal pensao, string deducaoNoImposto)
    {
        var formulas = new List<FormulaDto>
        {
            new("Regra da pensão", regra.EhPercentual ? regra.Descrever(Formato.Moeda, Formato.Percentual) : $"Valor informado de {Formato.Moeda(regra.Valor)}")
        };
        if (regra.Base == BasePensao.RendimentosLiquidos)
        {
            var descontos = inss is { } valorInss ? $" - {Formato.Moeda(valorInss)} (INSS)" : "";
            formulas.Add(new("Base da pensão", $"{Formato.Moeda(verba)} ({rotuloVerba}){descontos} - {Formato.Moeda(imposto)} (IRRF) = {Formato.Moeda(basePensao)}"));
        }
        else if (regra.Base == BasePensao.RendimentosBrutos)
            formulas.Add(new("Base da pensão", $"{Formato.Moeda(verba)} ({rotuloVerba})"));
        if (regra.EhPercentual)
            formulas.Add(new("Pensão", $"{Formato.Moeda(basePensao)} x {Formato.Percentual(regra.Percentual)} = {Formato.Moeda(pensao)}"));
        formulas.Add(new("Dedução no IRRF", deducaoNoImposto));
        return new GrupoMemoriaDto(titulo, $"Pensão: {Formato.Moeda(pensao)}", formulas);
    }

    /// <summary>Referência da pensão no demonstrativo: o percentual, ou vazio no valor informado.</summary>
    public static string Referencia(RegraPensao regra) => regra.EhPercentual ? Formato.PercentualCurto(regra.Percentual) : "";
}
