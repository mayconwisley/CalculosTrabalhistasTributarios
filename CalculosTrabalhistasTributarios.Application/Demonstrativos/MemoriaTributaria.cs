using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>Grupos da memória de cálculo do INSS e do IRRF, comuns aos demonstrativos das calculadoras.</summary>
internal static class MemoriaTributaria
{
    /// <summary>Previdência complementar deduzida por inteiro na base mensal, no holerite e nas férias.</summary>
    public const string ObservacaoPrevidenciaMensal = "A previdência complementar (PGBL, fundo de pensão ou Fapi) é deduzida por inteiro da base mensal do IRRF, só nas deduções legais (IN RFB 1.500/2014, art. 52, IV e V); o desconto simplificado substitui essa dedução. O limite de 12% dos rendimentos tributáveis é aplicado na declaração anual (art. 72, § 1º), e a dedução exige que o trabalhador também contribua para o INSS ou para o regime próprio. A contribuição não reduz o INSS, o FGTS nem a base da pensão.";

    /// <summary>Nome da verba do IRRF no demonstrativo, com a modalidade aplicada.</summary>
    public static string DescricaoIrrf(string verba, ApuracaoIrrf irrf) => $"{verba} ({(irrf.SimplificadaAplicada ? "desconto simplificado" : "deduções legais")})";

    /// <summary>Alíquota da faixa aplicada; vazia quando não há imposto, por exemplo pela redução mensal.</summary>
    public static string ReferenciaIrrf(ApuracaoIrrf irrf) => irrf.Imposto > 0m ? Formato.PercentualCurto(irrf.Aplicada.Aliquota) : string.Empty;

    public static GrupoMemoriaDto Inss(string titulo, ApuracaoInss inss, string rotuloBase)
    {
        var formulas = new List<FormulaDto>
        {
            new("Base de cálculo", inss.LimitadaAoTeto
                ? $"{Formato.Moeda(inss.BaseInformada)} ({rotuloBase}), limitada ao teto de {Formato.Moeda(inss.BaseConsiderada)}"
                : $"{Formato.Moeda(inss.BaseConsiderada)} ({rotuloBase})")
        };
        formulas.AddRange(inss.Detalhes.Select(faixa => new FormulaDto($"Faixa {faixa.Faixa}", $"{Formato.Moeda(faixa.BaseCalculada)} x {Formato.Percentual(faixa.Aliquota)} = {Formato.Moeda(faixa.Imposto)}")));
        if (inss.Detalhes.Count > 1)
            formulas.Add(new("Total", $"{string.Join(" + ", inss.Detalhes.Select(faixa => Formato.Moeda(faixa.Imposto)))} = {Formato.Moeda(inss.Valor)}"));
        return new GrupoMemoriaDto(titulo, $"INSS: {Formato.Moeda(inss.Valor)}", formulas);
    }

    /// <param name="rotuloRendimentos">Como o rendimento aparece na fórmula da base, por exemplo "férias + 1/3".</param>
    public static GrupoMemoriaDto Irrf(string titulo, ApuracaoIrrf irrf, string rotuloRendimentos)
    {
        var formulas = new List<FormulaDto>
        {
            new("Base com deduções legais", FormulaBaseIrrf.Normal(irrf.Rendimentos, rotuloRendimentos, irrf.Inss, irrf.Dependentes, irrf.DeducaoPorDependente, irrf.Normal.BaseCalculo, Formato.Moeda, irrf.Pensao, irrf.PrevidenciaComplementar)),
            new("Imposto com deduções legais", Imposto(irrf.Normal))
        };
        if (irrf.DescontoSimplificado is not null)
        {
            var simplificada = FormulaBaseIrrf.Simplificada(irrf.Rendimentos, rotuloRendimentos, irrf.DescontoSimplificado, irrf.Simplificada.BaseCalculo, Formato.Moeda);
            var naoDeduzidas = (irrf.Pensao > 0m, irrf.PrevidenciaComplementar > 0m) switch
            {
                (true, true) => "; a pensão e a previdência complementar não são deduzidas nesta modalidade",
                (true, false) => "; a pensão não é deduzida nesta modalidade",
                (false, true) => "; a previdência complementar não é deduzida nesta modalidade",
                _ => ""
            };
            formulas.Add(new("Base com desconto simplificado", simplificada + naoDeduzidas));
            formulas.Add(new("Imposto com desconto simplificado", Imposto(irrf.Simplificada)));
        }
        formulas.Add(new("Modalidade aplicada", DescreverModalidade(irrf)));
        if (irrf.RetencaoDispensada)
            formulas.Add(new("Retenção dispensada", DispensaDeRetencao(irrf.ImpostoCalculado, irrf.LimiteDispensa)));
        return new GrupoMemoriaDto(titulo, $"IRRF: {Formato.Moeda(irrf.Imposto)}", formulas);
    }

    /// <summary>Explicação do IRRF calculado que não é descontado por não passar do limite de retenção.</summary>
    public static string DispensaDeRetencao(decimal calculado, decimal limite) =>
        $"O IRRF calculado de {Formato.Moeda(calculado)} não passa de {Formato.Moeda(limite)} e não é retido (Lei 9.430/1996, art. 67): o desconto é de {Formato.Moeda(0m)}.";

    private static string Imposto(ModalidadeIrrf modalidade) =>
        modalidade.Aliquota == 0m
            ? $"{Formato.Moeda(modalidade.BaseCalculo)} está na faixa isenta: {Formato.Moeda(0m)}"
            : modalidade.ReducaoMensal > 0m
            ? $"{Formato.Moeda(modalidade.BaseCalculo)} x {Formato.Percentual(modalidade.Aliquota)} - {Formato.Moeda(modalidade.Deducao)} = {Formato.Moeda(modalidade.ImpostoAntesReducao)}; menos a redução mensal de {Formato.Moeda(modalidade.ReducaoMensal)} = {Formato.Moeda(modalidade.Imposto)}"
            : $"{Formato.Moeda(modalidade.BaseCalculo)} x {Formato.Percentual(modalidade.Aliquota)} - {Formato.Moeda(modalidade.Deducao)} = {Formato.Moeda(modalidade.Imposto)}";

    private static string DescreverModalidade(ApuracaoIrrf irrf)
    {
        if (irrf.DescontoSimplificado is null)
            return "Deduções legais: o desconto simplificado só vale a partir de 05/2023.";
        if (irrf.Normal.Imposto == irrf.Simplificada.Imposto)
            return $"As duas modalidades resultam em {Formato.Moeda(irrf.ImpostoCalculado)}.";
        return irrf.SimplificadaAplicada
            ? $"Desconto simplificado, que resulta em imposto menor ({Formato.Moeda(irrf.Simplificada.Imposto)} contra {Formato.Moeda(irrf.Normal.Imposto)})."
            : $"Deduções legais, que resultam em imposto menor ({Formato.Moeda(irrf.Normal.Imposto)} contra {Formato.Moeda(irrf.Simplificada.Imposto)}).";
    }
}
