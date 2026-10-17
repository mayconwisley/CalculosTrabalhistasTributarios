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

    /// <summary>
    /// Alíquota da última faixa alcançada, como no holerite, e não a efetiva: no progressivo, R$ 8.475,55 aparece na faixa de
    /// 14%, embora o desconto seja 11,66% da base. Vazia quando não há INSS.
    /// </summary>
    public static string ReferenciaInss(ApuracaoInss inss) =>
        inss.Valor > 0m && inss.Detalhes.LastOrDefault(faixa => faixa.BaseCalculada > 0m) is { } faixa ? Formato.PercentualCurto(faixa.Aliquota) : string.Empty;

    /// <summary>INSS faixa a faixa, com os limites de cada faixa, a parte da base que cai nela e o truncamento.</summary>
    public static GrupoMemoriaDto Inss(string titulo, ApuracaoInss inss, string rotuloBase) => MemoriaTributariaDetalhada.Inss(titulo, inss, rotuloBase);

    /// <param name="rotuloRendimentos">Como o rendimento aparece na fórmula da base, por exemplo "férias + 1/3".</param>
    public static GrupoMemoriaDto Irrf(string titulo, ApuracaoIrrf irrf, string rotuloRendimentos)
    {
        var formulas = new List<FormulaDto>();
        if (irrf.Dependentes > 0)
            formulas.Add(new("Dependentes", MemoriaTributariaDetalhada.Dependentes(irrf)));
        formulas.AddRange(
        [
            new("Base com deduções legais", FormulaBaseIrrf.Normal(irrf.Rendimentos, rotuloRendimentos, irrf.Inss, irrf.Dependentes, irrf.DeducaoPorDependente, irrf.Normal.BaseCalculo, Formato.Moeda, irrf.Pensao, irrf.PrevidenciaComplementar, irrf.LivroCaixa)),
            new("Imposto com deduções legais", Imposto(irrf.Normal))
        ]);
        if (irrf.DescontoSimplificado is not null)
        {
            var simplificada = FormulaBaseIrrf.Simplificada(irrf.Rendimentos, rotuloRendimentos, irrf.DescontoSimplificado, irrf.Simplificada.BaseCalculo, Formato.Moeda);
            var deducoes = new List<string>();
            if (irrf.Pensao > 0m) deducoes.Add("a pensão");
            if (irrf.PrevidenciaComplementar > 0m) deducoes.Add("a previdência complementar");
            if (irrf.LivroCaixa > 0m) deducoes.Add("o livro-caixa");
            // Concordância: "deduzida(s)" quando só há pensão e previdência, "deduzido(s)" quando entra o livro-caixa.
            var masculino = irrf.LivroCaixa > 0m;
            var verbo = deducoes.Count == 1 ? (masculino ? "é deduzido" : "é deduzida") : (masculino ? "são deduzidos" : "são deduzidas");
            var naoDeduzidas = deducoes.Count == 0 ? "" : $"; {Formato.Lista(deducoes)} não {verbo} nesta modalidade";
            formulas.Add(new("Base com desconto simplificado", simplificada + naoDeduzidas));
            formulas.Add(new("Imposto com desconto simplificado", Imposto(irrf.Simplificada)));
            if (MemoriaTributariaDetalhada.ComparacaoDasDeducoes(irrf) is { } comparacao)
                formulas.Add(comparacao);
        }
        formulas.Add(new("Modalidade aplicada", DescreverModalidade(irrf)));
        if (irrf.RetencaoDispensada)
            formulas.Add(new("Retenção dispensada", DispensaDeRetencao(irrf.ImpostoCalculado, irrf.LimiteDispensa)));
        return new GrupoMemoriaDto(titulo, $"IRRF: {Formato.Moeda(irrf.Imposto)}", formulas);
    }

    /// <summary>
    /// Base do IRRF e, em seguida, o imposto faixa a faixa da modalidade aplicada, com a conferência pela parcela a deduzir
    /// e a redução mensal.
    /// </summary>
    /// <param name="rotuloRendimentos">Como o rendimento aparece na fórmula da base, por exemplo "férias + 1/3".</param>
    public static IEnumerable<GrupoMemoriaDto> IrrfComFaixas(string titulo, ApuracaoIrrf irrf, string rotuloRendimentos)
    {
        yield return Irrf(titulo, irrf, rotuloRendimentos);
        if (FaixasIrrf(titulo, irrf) is { } faixas)
            yield return faixas;
    }

    /// <summary>IRRF faixa a faixa da modalidade aplicada; nulo sem base de cálculo ou sem a tabela usada.</summary>
    public static GrupoMemoriaDto? FaixasIrrf(string titulo, ApuracaoIrrf irrf) =>
        irrf.Aplicada.DetalhesProgressivos.Count == 0
            ? null
            : MemoriaTributariaDetalhada.FaixasIrrf($"{titulo} por faixas ({(irrf.SimplificadaAplicada ? "desconto simplificado" : "deduções legais")})", irrf.Aplicada, irrf);

    /// <summary>Explicação do IRRF calculado que não é descontado por não passar do limite de retenção.</summary>
    public static string DispensaDeRetencao(decimal calculado, decimal limite) =>
        $"O IRRF calculado de {Formato.Moeda(calculado)} não passa de {Formato.Moeda(limite)} e não é retido (Lei 9.430/1996, art. 67): o desconto é de {Formato.Moeda(0m)}.";

    private static string Imposto(ModalidadeIrrf modalidade) =>
        modalidade.Aliquota == 0m
            ? $"{Formato.Moeda(modalidade.BaseCalculo)} está na faixa isenta: {Formato.Moeda(0m)}"
            : modalidade.ReducaoMensal > 0m
            ? $"{Formato.Moeda(modalidade.BaseCalculo)} x {Formato.Percentual(modalidade.Aliquota)} - {Formato.Moeda(modalidade.Deducao)} = {Formato.Moeda(modalidade.ImpostoAntesReducao)}; menos a redução mensal de {Formato.Moeda(modalidade.ReducaoMensal)} = {Formato.Moeda(modalidade.Imposto)}"
            : $"{Formato.Moeda(modalidade.BaseCalculo)} x {Formato.Percentual(modalidade.Aliquota)} - {Formato.Moeda(modalidade.Deducao)} = {Formato.Moeda(modalidade.Imposto)}";

    /// <summary>Qual modalidade a fonte pagadora aplica e por quê.</summary>
    public static string DescreverModalidade(ApuracaoIrrf irrf)
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
