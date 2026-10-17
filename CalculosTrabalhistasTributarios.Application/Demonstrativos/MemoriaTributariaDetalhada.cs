using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>
/// Memória passo a passo do INSS e do IRRF: as tabelas da competência, a parte da base que cai em cada faixa, o produto
/// exato antes de truncar ou arredondar, a formação da base do IRRF nas duas modalidades, a conferência pela parcela a
/// deduzir e a redução mensal. Usa as apurações já feitas pelo domínio, com a tabela que cada uma usou; aqui só se explica
/// cada número.
/// </summary>
internal static class MemoriaTributariaDetalhada
{
    public static GrupoMemoriaDto TabelaInss(TabelasDaCompetencia tabelas)
    {
        var faixas = tabelas.FaixasInss;
        var formulas = faixas
            .Select((faixa, indice) => new FormulaDto($"Faixa {faixa.Numero}", $"{Intervalo(faixas, indice, ultimaSemLimite: false)}: {Formato.PercentualCurto(faixa.Aliquota)}"))
            .ToList();
        formulas.Add(new("Teto", $"{Formato.Moeda(tabelas.TetoInss)}: a parte da base acima do teto não tem desconto"));
        formulas.Add(new("Como se aplica", CalculadoraInss.Progressivo(tabelas.Competencia)
            ? "Progressivo (EC 103/2019, art. 28): a base é dividida pelas faixas e cada parte paga a alíquota da sua faixa. O valor de cada faixa é truncado nos centavos, como no eSocial, e o INSS é a soma das faixas."
            : "Alíquota única (antes de 03/2020): a base se enquadra em uma faixa, e a alíquota dessa faixa incide sobre a base inteira. O valor é truncado nos centavos."));
        return new GrupoMemoriaDto($"Tabela do INSS - {Formato.Competencia(tabelas.Competencia)}", $"Teto: {Formato.Moeda(tabelas.TetoInss)}", formulas);
    }

    /// <param name="rotuloBase">Origem da base na fórmula, por exemplo "base de INSS".</param>
    public static GrupoMemoriaDto Inss(string titulo, ApuracaoInss inss, string rotuloBase)
    {
        var destaque = $"INSS: {Formato.Moeda(inss.Valor)}";
        if (inss.Tabela is not { Count: > 0 } faixas)
            return InssSemTabela(titulo, inss, rotuloBase, destaque);

        var teto = faixas[^1].Limite;
        var formulas = new List<FormulaDto>
        {
            new("Base de cálculo", inss.LimitadaAoTeto
                ? $"{Formato.Moeda(inss.BaseInformada)} ({rotuloBase}), limitada ao teto: {Formato.Moeda(inss.BaseConsiderada)}. Os {Formato.Moeda(inss.BaseInformada - inss.BaseConsiderada)} acima do teto não têm INSS"
                : $"{Formato.Moeda(inss.BaseConsiderada)} ({rotuloBase}), abaixo do teto de {Formato.Moeda(teto)}")
        };
        if (inss.Detalhes.Count == 0)
        {
            formulas.Add(new("INSS", $"Sem base de cálculo: {Formato.Moeda(0m)}"));
            return new GrupoMemoriaDto(titulo, destaque, formulas);
        }

        if (!inss.Progressivo)
        {
            var unica = inss.Detalhes[0];
            var indice = IndiceDaFaixa(faixas, unica.Faixa);
            formulas.Add(new($"Faixa {unica.Faixa} ({Formato.PercentualCurto(unica.Aliquota)})",
                $"A base está na faixa {unica.Faixa} ({Minuscula(Intervalo(faixas, indice, ultimaSemLimite: false))}), e a alíquota vale para a base inteira: {Produto(unica.BaseCalculada, unica.Aliquota)}, {Truncamento(unica)}"));
        }
        else
        {
            foreach (var faixa in inss.Detalhes)
                formulas.Add(new($"Faixa {faixa.Faixa} ({Formato.PercentualCurto(faixa.Aliquota)})",
                    $"{ParteDaBase(faixas, faixa, inss.BaseConsiderada)}; {Produto(faixa.BaseCalculada, faixa.Aliquota)}, {Truncamento(faixa)}"));
            if (NaoAlcancadas(faixas, inss.Detalhes) is { } naoAlcancadas)
                formulas.Add(naoAlcancadas);
            formulas.Add(new("Total", inss.Detalhes.Count == 1
                ? $"Só a faixa 1: {Formato.Moeda(inss.Valor)}"
                : $"{string.Join(" + ", inss.Detalhes.Select(faixa => Formato.Moeda(faixa.Imposto)))} = {Formato.Moeda(inss.Valor)}"));
        }
        if (inss.BaseConsiderada > 0m)
            formulas.Add(new("Alíquota efetiva", $"{Formato.Moeda(inss.Valor)} ÷ {Formato.Moeda(inss.BaseConsiderada)} = {Formato.Percentual(Math.Round(inss.Valor / inss.BaseConsiderada * 100m, 2, MidpointRounding.AwayFromZero))} da base"));
        // Na alíquota única não há partes da base por faixa para pôr em tabela.
        return new GrupoMemoriaDto(titulo, destaque, formulas,
            inss.Progressivo ? Quadro(faixas, inss.Detalhes, inss.BaseConsiderada, ultimaSemLimite: false, inss.Valor) : null);
    }

    public static GrupoMemoriaDto TabelaIrrf(TabelasDaCompetencia tabelas)
    {
        var faixas = tabelas.FaixasIrrf;
        var formulas = faixas
            .Select((faixa, indice) => new FormulaDto($"Faixa {faixa.Numero}", faixa.Aliquota == 0m
                ? $"{Intervalo(faixas, indice, ultimaSemLimite: true)}: isento"
                : $"{Intervalo(faixas, indice, ultimaSemLimite: true)}: {Formato.PercentualCurto(faixa.Aliquota)}, parcela a deduzir de {Formato.Moeda(faixa.Deducao)}"))
            .ToList();
        formulas.Add(new("Dedução por dependente", $"{Formato.Moeda(tabelas.DeducaoPorDependente)} por dependente, só nas deduções legais"));
        formulas.Add(new("Desconto simplificado", tabelas.DescontoSimplificado is { } desconto
            ? $"{Formato.Moeda(desconto)}, no lugar de todas as deduções legais (INSS, dependentes, pensão e previdência complementar)"
            : "Não existia na competência: só a partir de 05/2023"));
        var regras = tabelas.RegrasReducaoMensal;
        for (var i = 0; i < regras.Count; i++)
        {
            var de = i == 0 ? $"Rendimentos até {Formato.Moeda(regras[i].LimiteRendimentos)}" : $"Rendimentos de {Formato.Moeda(regras[i - 1].LimiteRendimentos + 0.01m)} a {Formato.Moeda(regras[i].LimiteRendimentos)}";
            formulas.Add(new($"Redução mensal {i + 1}", regras[i].Multiplicador == 0m
                ? $"{de}: até {Formato.Moeda(regras[i].ValorBase)}, limitada ao imposto apurado"
                : $"{de}: {Formato.Moeda(regras[i].ValorBase)} - {Formato.Fator(regras[i].Multiplicador)} x rendimentos tributáveis"));
        }
        if (regras.Count > 0)
            formulas.Add(new("Sem redução", $"Rendimentos acima de {Formato.Moeda(regras[^1].LimiteRendimentos)} (Lei 15.270/2025). A redução se mede pelos rendimentos tributáveis, não pela base de cálculo"));
        return new GrupoMemoriaDto($"Tabela do IRRF - {Formato.Competencia(tabelas.Competencia)}", $"Dependente: {Formato.Moeda(tabelas.DeducaoPorDependente)}", formulas);
    }

    /// <param name="rotuloRendimentos">Origem dos rendimentos na fórmula, por exemplo "valor bruto".</param>
    public static GrupoMemoriaDto BaseIrrf(ApuracaoIrrf irrf, string rotuloRendimentos)
    {
        var formulas = new List<FormulaDto> { new("Rendimentos tributáveis", $"{Formato.Moeda(irrf.Rendimentos)} ({rotuloRendimentos})") };
        if (irrf.Inss > 0m)
            formulas.Add(new("INSS", $"{Formato.Moeda(irrf.Inss)}, a contribuição descontada no mês"));
        formulas.Add(new("Dependentes", irrf.Dependentes == 0 ? "Sem dependentes: nenhuma dedução" : Dependentes(irrf)));
        if (irrf.Pensao > 0m)
            formulas.Add(new("Pensão alimentícia", Formato.Moeda(irrf.Pensao)));
        if (irrf.PrevidenciaComplementar > 0m)
            formulas.Add(new("Previdência complementar", Formato.Moeda(irrf.PrevidenciaComplementar)));
        if (irrf.LivroCaixa > 0m)
            formulas.Add(new("Livro-caixa", Formato.Moeda(irrf.LivroCaixa)));
        formulas.Add(new("Base com deduções legais", FormulaBaseIrrf.Normal(irrf.Rendimentos, rotuloRendimentos, irrf.Inss, irrf.Dependentes, irrf.DeducaoPorDependente, irrf.Normal.BaseCalculo, Formato.Moeda, irrf.Pensao, irrf.PrevidenciaComplementar, irrf.LivroCaixa)));
        formulas.Add(new("Base com desconto simplificado", FormulaBaseIrrf.Simplificada(irrf.Rendimentos, rotuloRendimentos, irrf.DescontoSimplificado, irrf.Simplificada.BaseCalculo, Formato.Moeda)));
        if (ComparacaoDasDeducoes(irrf) is { } comparacao)
            formulas.Add(comparacao);
        return new GrupoMemoriaDto("Base de cálculo do IRRF", $"Base aplicada: {Formato.Moeda(irrf.Aplicada.BaseCalculo)}", formulas);
    }

    /// <summary>"2 x R$ 189,59 = R$ 379,18": a dedução dos dependentes nas deduções legais.</summary>
    public static string Dependentes(ApuracaoIrrf irrf) =>
        $"{irrf.Dependentes} x {Formato.Moeda(irrf.DeducaoPorDependente)} = {Formato.Moeda(irrf.Dependentes * irrf.DeducaoPorDependente)}";

    /// <summary>Total das deduções legais contra o desconto simplificado; nulo antes de 05/2023, sem o desconto.</summary>
    public static FormulaDto? ComparacaoDasDeducoes(ApuracaoIrrf irrf)
    {
        if (irrf.DescontoSimplificado is not { } desconto)
            return null;
        var deducoesLegais = irrf.Inss + irrf.Dependentes * irrf.DeducaoPorDependente + irrf.Pensao + irrf.PrevidenciaComplementar + irrf.LivroCaixa;
        return new("Comparação das deduções", deducoesLegais == desconto
            ? $"As deduções legais somam {Formato.Moeda(deducoesLegais)}, o mesmo que o desconto simplificado"
            : $"As deduções legais somam {Formato.Moeda(deducoesLegais)}, e o desconto simplificado é de {Formato.Moeda(desconto)}: a base {(deducoesLegais > desconto ? "com deduções legais" : "com desconto simplificado")} é menor");
    }

    /// <summary>
    /// Imposto da modalidade pela tabela progressiva, faixa a faixa e pela parcela a deduzir, e a redução mensal. Nulo
    /// quando a apuração não traz a tabela usada.
    /// </summary>
    public static GrupoMemoriaDto? FaixasIrrf(string titulo, ModalidadeIrrf modalidade, ApuracaoIrrf irrf)
    {
        if (irrf.Tabela is not { Count: > 0 } faixas)
            return null;
        var baseCalculo = modalidade.BaseCalculo;
        var destaque = $"IRRF: {Formato.Moeda(modalidade.Imposto)}";
        if (modalidade.DetalhesProgressivos.Count == 0)
            return new GrupoMemoriaDto(titulo, destaque, [new("Base de cálculo", $"{Formato.Moeda(0m)}: sem imposto")]);

        var (formulas, quadro) = FaixasProgressivas(faixas, modalidade.DetalhesProgressivos, baseCalculo, modalidade.Aliquota, modalidade.Deducao, modalidade.ImpostoAntesReducao);
        if (irrf.RegrasReducao is { Count: > 0 } regras)
            formulas.Add(new("Redução mensal", Reducao(modalidade, irrf.Rendimentos, regras)));
        var resultado = modalidade.ReducaoMensal > 0m
            ? $"{Formato.Moeda(modalidade.ImpostoAntesReducao)} - {Formato.Moeda(modalidade.ReducaoMensal)} (redução) = {Formato.Moeda(modalidade.Imposto)}"
            : Formato.Moeda(modalidade.Imposto);
        if (irrf.Rendimentos > 0m)
            resultado += $", alíquota efetiva de {Formato.Percentual(modalidade.AliquotaEfetiva)} dos rendimentos tributáveis";
        formulas.Add(new("IRRF da modalidade", resultado));
        return new GrupoMemoriaDto(titulo, destaque, formulas, quadro);
    }

    /// <summary>
    /// Imposto de uma tabela com parcela a deduzir explicado faixa a faixa: a base, a parte dela em cada faixa com o
    /// produto exato, a soma e a conferência pela parcela a deduzir, além do quadro com as mesmas faixas. Serve ao IRRF
    /// mensal, ao da pensão e ao da tabela anual da PLR.
    /// </summary>
    /// <param name="faixas">Tabela usada, da menor para a maior faixa; a última não tem limite.</param>
    /// <param name="detalhes">Parte da base e imposto exato de cada faixa alcançada, sem arredondar; ao menos uma faixa.</param>
    /// <param name="imposto">Imposto calculado pela parcela a deduzir, o que vale na retenção.</param>
    public static (List<FormulaDto> Formulas, QuadroFaixasDto Quadro) FaixasProgressivas(IReadOnlyList<FaixaTributaria> faixas,
        IReadOnlyList<ResultadoFaixaTributaria> detalhes, decimal baseCalculo, decimal aliquota, decimal deducao, decimal imposto)
    {
        var indiceAlcancado = IndiceDaFaixa(faixas, detalhes[^1].Faixa);
        var formulas = new List<FormulaDto>
        {
            new("Base de cálculo", $"{Formato.Moeda(baseCalculo)}, na faixa {faixas[indiceAlcancado].Numero} ({Minuscula(Intervalo(faixas, indiceAlcancado, ultimaSemLimite: true))})")
        };
        foreach (var faixa in detalhes)
            formulas.Add(new($"Faixa {faixa.Faixa} ({(faixa.Aliquota == 0m ? "isenta" : Formato.PercentualCurto(faixa.Aliquota))})",
                $"{ParteDaBase(faixas, faixa, baseCalculo)}; {Produto(faixa.BaseCalculada, faixa.Aliquota)}"));
        if (NaoAlcancadas(faixas, detalhes) is { } naoAlcancadas)
            formulas.Add(naoAlcancadas);

        var soma = detalhes.Sum(faixa => faixa.Imposto);
        var somaArredondada = CalculadoraTributacao.Arredondar(soma);
        if (detalhes.Count > 1)
            formulas.Add(new("Soma das faixas", $"{string.Join(" + ", detalhes.Select(faixa => Formato.MoedaExata(faixa.Imposto)))} = {Formato.MoedaExata(soma)}, arredondada para {Formato.Moeda(somaArredondada)}"));
        if (aliquota == 0m)
            formulas.Add(new("Imposto", $"A base está na faixa isenta: {Formato.Moeda(0m)}"));
        else
        {
            var conferencia = $"{Formato.Moeda(baseCalculo)} x {Formato.PercentualCurto(aliquota)} - {Formato.Moeda(deducao)} = {Formato.Moeda(imposto)}. A parcela a deduzir tira o que as faixas anteriores pagariam a mais se a base inteira tivesse a alíquota de {Formato.PercentualCurto(aliquota)}";
            formulas.Add(new("Pela parcela a deduzir", somaArredondada == imposto
                ? $"{conferencia}; o resultado é o mesmo da soma das faixas"
                : $"{conferencia}. A soma das faixas dá {Formato.Moeda(somaArredondada)}: a diferença vem do arredondamento da parcela a deduzir publicada, e vale o cálculo pela parcela a deduzir, que é o da fonte pagadora"));
        }
        return (formulas, Quadro(faixas, detalhes, baseCalculo, ultimaSemLimite: true, somaArredondada));
    }

    /// <param name="tributacaoExclusiva">O rendimento é o 13º, tributado à parte e retido mesmo abaixo de R$ 10,00.</param>
    public static GrupoMemoriaDto Retido(string titulo, ApuracaoIrrf irrf, bool tributacaoExclusiva)
    {
        var formulas = new List<FormulaDto> { new("Modalidade aplicada", MemoriaTributaria.DescreverModalidade(irrf)) };
        if (irrf.RetencaoDispensada)
            formulas.Add(new("Retenção dispensada", MemoriaTributaria.DispensaDeRetencao(irrf.ImpostoCalculado, irrf.LimiteDispensa)));
        else if (tributacaoExclusiva && irrf.ImpostoCalculado > 0m)
            formulas.Add(new("Retenção", "Tributação exclusiva na fonte: não há a dispensa de retenção do IRRF de até R$ 10,00"));
        formulas.Add(new("Desconto", Formato.Moeda(irrf.Imposto)));
        return new GrupoMemoriaDto(titulo, $"IRRF: {Formato.Moeda(irrf.Imposto)}", formulas);
    }

    private static GrupoMemoriaDto InssSemTabela(string titulo, ApuracaoInss inss, string rotuloBase, string destaque)
    {
        var formulas = new List<FormulaDto>
        {
            new("Base de cálculo", inss.LimitadaAoTeto
                ? $"{Formato.Moeda(inss.BaseInformada)} ({rotuloBase}), limitada ao teto de {Formato.Moeda(inss.BaseConsiderada)}"
                : $"{Formato.Moeda(inss.BaseConsiderada)} ({rotuloBase})")
        };
        formulas.AddRange(inss.Detalhes.Select(faixa => new FormulaDto($"Faixa {faixa.Faixa}", $"{Produto(faixa.BaseCalculada, faixa.Aliquota)}, {Truncamento(faixa)}")));
        if (inss.Detalhes.Count > 1)
            formulas.Add(new("Total", $"{string.Join(" + ", inss.Detalhes.Select(faixa => Formato.Moeda(faixa.Imposto)))} = {Formato.Moeda(inss.Valor)}"));
        return new GrupoMemoriaDto(titulo, destaque, formulas);
    }

    private static string Reducao(ModalidadeIrrf modalidade, decimal rendimentos, IReadOnlyList<RegraReducaoMensalIrrf> regras)
    {
        var limite = regras[^1].LimiteRendimentos;
        if (rendimentos > limite)
            return $"Rendimentos tributáveis de {Formato.Moeda(rendimentos)}, acima de {Formato.Moeda(limite)}: sem redução";
        if (modalidade.ImpostoAntesReducao == 0m)
            return "Sem imposto a reduzir";
        // Mesma escolha de regra do domínio: a primeira cujo limite alcança os rendimentos.
        var regra = regras.First(item => rendimentos <= item.LimiteRendimentos);
        var limitada = modalidade.ReducaoMensal < modalidade.ImpostoAntesReducao ? "" : $", limitada ao imposto de {Formato.Moeda(modalidade.ImpostoAntesReducao)}: {Formato.Moeda(modalidade.ReducaoMensal)}";
        if (regra.Multiplicador == 0m)
            return $"Rendimentos tributáveis de {Formato.Moeda(rendimentos)}, até {Formato.Moeda(regra.LimiteRendimentos)}: até {Formato.Moeda(regra.ValorBase)}{limitada}";
        var calculada = regra.ValorBase - regra.Multiplicador * rendimentos;
        var arredondada = CalculadoraTributacao.Arredondar(calculada);
        return $"Rendimentos tributáveis de {Formato.Moeda(rendimentos)}: {Formato.Moeda(regra.ValorBase)} - {Formato.Fator(regra.Multiplicador)} x {Formato.Moeda(rendimentos)} = {Formato.MoedaExata(calculada)}{(arredondada == calculada ? "" : $", arredondada para {Formato.Moeda(arredondada)}")}{limitada}";
    }

    /// <summary>"Até R$ 1.621,00", "De R$ 1.621,01 a R$ 2.902,84" ou, na última faixa sem limite, "Acima de R$ 4.664,68".</summary>
    private static string Intervalo(IReadOnlyList<FaixaTributaria> faixas, int indice, bool ultimaSemLimite)
    {
        if (indice == 0)
            return $"Até {Formato.Moeda(faixas[0].Limite)}";
        var anterior = faixas[indice - 1].Limite;
        return ultimaSemLimite && indice == faixas.Count - 1
            ? $"Acima de {Formato.Moeda(anterior)}"
            : $"De {Formato.Moeda(anterior + 0.01m)} a {Formato.Moeda(faixas[indice].Limite)}";
    }

    /// <summary>Como a parte da base que cai na faixa foi obtida: o limite da faixa, ou a base, menos o limite da anterior.</summary>
    private static string ParteDaBase(IReadOnlyList<FaixaTributaria> faixas, ResultadoFaixaTributaria faixa, decimal baseCalculo)
    {
        var indice = IndiceDaFaixa(faixas, faixa.Faixa);
        var termina = baseCalculo <= faixas[indice].Limite;
        if (indice == 0)
            return termina ? $"A base inteira fica na faixa: {Formato.Moeda(faixa.BaseCalculada)}" : $"Parte da base até {Formato.Moeda(faixas[0].Limite)}: {Formato.Moeda(faixa.BaseCalculada)}";
        var anterior = faixas[indice - 1].Limite;
        return termina
            ? $"Parte acima de {Formato.Moeda(anterior)}: {Formato.Moeda(baseCalculo)} (base) - {Formato.Moeda(anterior)} = {Formato.Moeda(faixa.BaseCalculada)}"
            : $"Parte de {Formato.Moeda(anterior + 0.01m)} a {Formato.Moeda(faixas[indice].Limite)}: {Formato.Moeda(faixas[indice].Limite)} - {Formato.Moeda(anterior)} = {Formato.Moeda(faixa.BaseCalculada)}";
    }

    private static FormulaDto? NaoAlcancadas(IReadOnlyList<FaixaTributaria> faixas, IReadOnlyList<ResultadoFaixaTributaria> detalhes)
    {
        var ultima = detalhes[^1].Faixa;
        var restantes = faixas.Where(faixa => faixa.Numero > ultima).Select(faixa => faixa.Numero.ToString(Formato.Cultura)).ToList();
        return restantes.Count switch
        {
            0 => null,
            1 => new FormulaDto($"Faixa {restantes[0]}", $"Não alcançada: a base termina na faixa {ultima}"),
            _ => new FormulaDto($"Faixas {Formato.Lista(restantes)}", $"Não alcançadas: a base termina na faixa {ultima}")
        };
    }

    /// <summary>Todas as faixas da tabela, com a parte da base de cada uma; as não alcançadas ficam com zero.</summary>
    /// <param name="valor">Total considerado: o INSS apurado ou a soma exata do IRRF arredondada.</param>
    public static QuadroFaixasDto Quadro(IReadOnlyList<FaixaTributaria> faixas, IReadOnlyList<ResultadoFaixaTributaria> detalhes, decimal baseCalculo,
        bool ultimaSemLimite, decimal valor)
    {
        var linhas = faixas.Select((faixa, indice) =>
        {
            var detalhe = detalhes.FirstOrDefault(item => item.Faixa == faixa.Numero);
            return new FaixaMemoriaDto(
                faixa.Numero,
                indice == 0 ? 0m : faixas[indice - 1].Limite + 0.01m,
                ultimaSemLimite && indice == faixas.Count - 1 ? null : faixa.Limite,
                detalhe?.BaseCalculada ?? 0m,
                faixa.Aliquota,
                detalhe is null ? 0m : detalhe.BaseCalculada * detalhe.Aliquota / 100m,
                detalhe?.Imposto ?? 0m);
        }).ToList();
        return new QuadroFaixasDto(linhas, baseCalculo, linhas.Sum(linha => linha.ValorExato), valor);
    }

    private static string Minuscula(string texto) => char.ToLower(texto[0], Formato.Cultura) + texto[1..];

    private static int IndiceDaFaixa(IReadOnlyList<FaixaTributaria> faixas, int numero)
    {
        for (var i = 0; i < faixas.Count; i++)
            if (faixas[i].Numero == numero)
                return i;
        throw new InvalidOperationException($"A faixa {numero} não está na tabela da competência.");
    }

    private static string Produto(decimal baseDaFaixa, decimal aliquota) =>
        $"{Formato.Moeda(baseDaFaixa)} x {Formato.PercentualCurto(aliquota)} = {Formato.MoedaExata(baseDaFaixa * aliquota / 100m)}";

    private static string Truncamento(ResultadoFaixaTributaria faixa) =>
        faixa.BaseCalculada * faixa.Aliquota / 100m == faixa.Imposto ? "sem centavos a truncar" : $"truncado em {Formato.Moeda(faixa.Imposto)}";
}
