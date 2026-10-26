using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Simulação tributária de uma competência: INSS progressivo sobre a base de INSS, IRRF nas duas modalidades sobre o valor
/// bruto, salário líquido e o FGTS do tipo de vínculo, que é depositado pelo empregador e não reduz o líquido. Na
/// aposentadoria ou pensão não há INSS nem FGTS, e, aos 65 anos ou mais, a parcela isenta sai da base do IRRF.
/// </summary>
public sealed class SimularImpostoUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularImpostoRequest>
{
    private const decimal LimiteValor = 1_000_000_000m;
    private const int LimiteDependentes = 99;
    private const decimal AliquotaCompensatoria = 3.2m;

    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularImpostoRequest request, CancellationToken cancellationToken)
    {
        if (Validar(request) is { Falhou: true } invalido)
            return invalido.Erro;
        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(request.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        if (request.Vinculo == VinculoSimulacao.Aposentadoria)
            return MontarAposentadoria(request, tabelas);
        var inss = tabelas.CalcularInss(request.BaseInss);
        var irrf = tabelas.CalcularIrrf(request.ValorBruto, inss.Valor, request.QuantidadeDependentes);
        return Montar(request, tabelas, inss, irrf);
    }

    private static Result Validar(SimularImpostoRequest r)
    {
        if (r.ValorBruto <= 0m || r.ValorBruto > LimiteValor)
            return Erro.Validacao($"Valor bruto: informe um valor maior que zero e até {Formato.Moeda(LimiteValor)}, por exemplo 3.500,00.");
        if (r.BaseInss < 0m || r.BaseInss > LimiteValor)
            return Erro.Validacao($"Base de INSS: informe um valor de zero a {Formato.Moeda(LimiteValor)}; em geral, é igual ao valor bruto.");
        if (r.QuantidadeDependentes is < 0 or > LimiteDependentes)
            return Erro.Validacao($"Dependentes: informe de 0 a {LimiteDependentes} dependentes para a dedução do IRRF.");
        if (!Enum.IsDefined(r.Vinculo))
            return Erro.Validacao("Tipo de vínculo: escolha empregado, aprendiz, doméstico ou aposentadoria ou pensão.");
        if (r.Vinculo == VinculoSimulacao.Aposentadoria && r.BaseInss != 0m)
            return Erro.Validacao("Base de INSS: aposentadoria e pensão não têm desconto de INSS; deixe a base em zero.");
        if (r.Vinculo != VinculoSimulacao.Aposentadoria && r.SessentaECincoAnos)
            return Erro.Validacao("65 anos ou mais: a parcela isenta vale só para aposentadoria ou pensão; escolha esse tipo de vínculo.");
        if (r.Vinculo != VinculoSimulacao.Aposentadoria && r.DecimoTerceiro)
            return Erro.Validacao("Rendimento: o 13º desta simulação é o da aposentadoria ou pensão; para o 13º do empregado, use a calculadora 13º salário.");
        if (r.SessentaECincoAnos && ParcelaIsentaAposentadoria.Mensal(r.Competencia) is { Falhou: true } semParcela)
            return semParcela.Erro;
        return Result.Ok();
    }

    private static DemonstrativoDto Montar(SimularImpostoRequest r, TabelasDaCompetencia tabelas, ApuracaoInss inss, ApuracaoIrrf irrf)
    {
        var liquido = r.ValorBruto - inss.Valor - irrf.Imposto;
        // O FGTS não tem teto: incide sobre toda a remuneração informada, e não sobre a base limitada do INSS.
        var categoria = r.Vinculo switch { VinculoSimulacao.Aprendiz => CategoriaFgts.Aprendiz, VinculoSimulacao.Domestico => CategoriaFgts.Domestico, _ => CategoriaFgts.Empregado };
        var aliquotaFgts = ConferenciaFgts.Aliquota(categoria);
        var fgts = CalculadoraTributacao.Arredondar(r.BaseInss * aliquotaFgts / 100m);
        var domestico = r.Vinculo == VinculoSimulacao.Domestico;
        var compensatoria = domestico ? CalculadoraTributacao.Arredondar(r.BaseInss * AliquotaCompensatoria / 100m) : 0m;
        var simplificadoDisponivel = irrf.DescontoSimplificado is not null;

        var destaques = new List<DestaqueDto>
        {
            new("Salário líquido", Formato.Moeda(liquido), irrf.RetencaoDispensada ? $"IRRF de {Formato.Moeda(irrf.ImpostoCalculado)} dispensado" : "Valor bruto menos INSS e IRRF"),
            new("INSS", Formato.Moeda(inss.Valor), inss.LimitadaAoTeto ? $"Base limitada ao teto de {Formato.Moeda(inss.BaseConsiderada)}" : $"Base: {Formato.Moeda(inss.BaseConsiderada)}"),
            new("IRRF", Formato.Moeda(irrf.Imposto), DescreverIrrf(irrf)),
            domestico
                ? new("FGTS e indenização", Formato.Moeda(fgts + compensatoria), $"8% ({Formato.Moeda(fgts)}) + 3,2% ({Formato.Moeda(compensatoria)})")
                : new($"FGTS ({Formato.PercentualCurto(aliquotaFgts)})", Formato.Moeda(fgts), r.Vinculo == VinculoSimulacao.Aprendiz ? "Contrato de aprendizagem" : "Depositado pelo empregador")
        };

        var informativos = new List<VerbaDto> { new("FGTS", Formato.PercentualCurto(aliquotaFgts), fgts) };
        if (domestico)
            informativos.Add(new("Indenização compensatória (FGTS)", "3,2%", compensatoria));

        var modalidades = new List<LinhaComparativaDto> { Linha("Deduções legais", irrf.Normal, !irrf.SimplificadaAplicada) };
        if (simplificadoDisponivel)
            modalidades.Add(Linha("Desconto simplificado", irrf.Simplificada, irrf.SimplificadaAplicada));
        var comparativo = new TabelaComparativaDto(simplificadoDisponivel ? "IRRF nas duas modalidades" : "IRRF com deduções legais",
            ["Base de cálculo", "Redução mensal", "IRRF", "Alíquota efetiva"], modalidades);

        var memoria = new List<GrupoMemoriaDto>
        {
            MemoriaTributariaDetalhada.TabelaInss(tabelas),
            MemoriaTributariaDetalhada.Inss("INSS por faixas", inss, "base de INSS"),
            MemoriaTributariaDetalhada.TabelaIrrf(tabelas),
            MemoriaTributariaDetalhada.BaseIrrf(irrf, "valor bruto")
        };
        memoria.AddRange(FaixasIrrf(irrf, tributacaoExclusiva: false));
        memoria.Add(new GrupoMemoriaDto("Salário líquido", $"Líquido: {Formato.Moeda(liquido)}",
            [new("Valor bruto - INSS - IRRF", $"{Formato.Moeda(r.ValorBruto)} - {Formato.Moeda(inss.Valor)} - {Formato.Moeda(irrf.Imposto)} = {Formato.Moeda(liquido)}")]));
        var formulasFgts = new List<FormulaDto> { new("FGTS", $"{Formato.Moeda(r.BaseInss)} (base de INSS, sem teto) x {Formato.PercentualCurto(aliquotaFgts)} = {Formato.Moeda(fgts)}") };
        if (domestico)
            formulasFgts.Add(new("Indenização compensatória", $"{Formato.Moeda(r.BaseInss)} x 3,2% = {Formato.Moeda(compensatoria)}"));
        memoria.Add(new GrupoMemoriaDto("FGTS", $"Depósito: {Formato.Moeda(fgts + compensatoria)}", formulasFgts));

        var observacoes = new List<string>
        {
            $"INSS e IRRF pelas tabelas da competência {Formato.Competencia(tabelas.Competencia)}. O INSS é progressivo, com cada faixa truncada nos centavos, e incide sobre a base de INSS até o teto; o IRRF incide sobre o valor bruto menos o INSS e as deduções.",
            Vantagem(irrf),
            r.Vinculo switch
            {
                VinculoSimulacao.Aprendiz => "Contrato de aprendizagem: FGTS de 2% da remuneração (Lei 8.036/1990, art. 15, § 7º). INSS e IRRF seguem as mesmas tabelas do empregado.",
                VinculoSimulacao.Domestico => "Empregado doméstico: o empregador deposita 8% de FGTS e mais 3,2% de indenização compensatória da perda do emprego, que o trabalhador saca na dispensa sem justa causa (LC 150/2015, arts. 22 e 34). Os encargos do empregador estão na calculadora Empregado doméstico (DAE).",
                _ => "FGTS de 8% da remuneração (Lei 8.036/1990, art. 15)."
            } + " O FGTS é depositado pelo empregador, não tem teto e não reduz o salário líquido."
        };
        if (r.BaseInss != r.ValorBruto)
            observacoes.Add($"A base de INSS ({Formato.Moeda(r.BaseInss)}) difere do valor bruto ({Formato.Moeda(r.ValorBruto)}): o INSS e o FGTS incidem sobre a base de INSS, e o IRRF, sobre o valor bruto.");
        observacoes.Add("Não considera pensão alimentícia, previdência complementar, outros descontos nem outros rendimentos do mês. Para o holerite completo, use a calculadora Holerite do mês.");

        var vinculo = r.Vinculo switch { VinculoSimulacao.Aprendiz => "aprendiz", VinculoSimulacao.Domestico => "doméstico", _ => "empregado" };
        return new DemonstrativoDto("Simulação tributária", $"Competência {Formato.Competencia(tabelas.Competencia)} • {vinculo} • {Dependentes(r)}",
            destaques,
            [new("Valor bruto", "", r.ValorBruto)],
            [
                new("INSS", MemoriaTributaria.ReferenciaInss(inss), inss.Valor),
                new(MemoriaTributaria.DescricaoIrrf("IRRF", irrf), MemoriaTributaria.ReferenciaIrrf(irrf), irrf.Imposto)
            ],
            informativos, memoria, observacoes,
            RotuloProventos: "Rendimentos", RotuloResultado: "Salário líquido", Comparativo: comparativo);
    }

    /// <summary>
    /// Aposentadoria ou pensão do INSS ou da previdência complementar: sem INSS e sem FGTS. Aos 65 anos ou mais, até
    /// R$ 1.903,98 do benefício é isento (Lei 7.713/1988, art. 6º, XV) e só o restante é rendimento tributável, nas duas
    /// modalidades e também para a redução mensal, que se mede pelos rendimentos tributáveis. O 13º (abono anual) é tributado
    /// à parte do benefício do mês, exclusivamente na fonte, e tem parcela isenta própria do mesmo valor.
    /// </summary>
    private static DemonstrativoDto MontarAposentadoria(SimularImpostoRequest r, TabelasDaCompetencia tabelas)
    {
        var isenta = r.SessentaECincoAnos ? Math.Min(r.ValorBruto, ParcelaIsentaAposentadoria.Valor) : 0m;
        var tributavel = r.ValorBruto - isenta;
        var irrf = tabelas.CalcularIrrf(tributavel, 0m, r.QuantidadeDependentes, tributacaoExclusiva: r.DecimoTerceiro);
        var liquido = r.ValorBruto - irrf.Imposto;
        var rotuloLiquido = r.DecimoTerceiro ? "13º líquido" : "Benefício líquido";
        var simplificadoDisponivel = irrf.DescontoSimplificado is not null;

        var destaques = new List<DestaqueDto>
        {
            new(rotuloLiquido, Formato.Moeda(liquido), irrf.RetencaoDispensada ? $"IRRF de {Formato.Moeda(irrf.ImpostoCalculado)} dispensado" : "Valor bruto menos IRRF"),
            new("IRRF", Formato.Moeda(irrf.Imposto), DescreverIrrf(irrf)),
            new("Parcela isenta", Formato.Moeda(isenta), !r.SessentaECincoAnos ? "Só a partir dos 65 anos" : r.DecimoTerceiro ? "Própria do 13º" : "65 anos ou mais"),
            new("Rendimento tributável", Formato.Moeda(tributavel), "Base do IRRF antes das deduções")
        };

        var modalidades = new List<LinhaComparativaDto> { Linha("Deduções legais", irrf.Normal, !irrf.SimplificadaAplicada) };
        if (simplificadoDisponivel)
            modalidades.Add(Linha("Desconto simplificado", irrf.Simplificada, irrf.SimplificadaAplicada));
        var comparativo = new TabelaComparativaDto(simplificadoDisponivel ? "IRRF nas duas modalidades" : "IRRF com deduções legais",
            ["Base de cálculo", "Redução mensal", "IRRF", "Alíquota efetiva"], modalidades);

        FormulaDto rendimento = r.SessentaECincoAnos
            ? new("Valor bruto - parcela isenta", $"{Formato.Moeda(r.ValorBruto)} - {Formato.Moeda(isenta)} (65 anos ou mais, até {Formato.Moeda(ParcelaIsentaAposentadoria.Valor)}) = {Formato.Moeda(tributavel)}")
            : new("Valor bruto", $"{Formato.Moeda(r.ValorBruto)}, sem parcela isenta antes dos 65 anos");
        var memoria = new List<GrupoMemoriaDto>
        {
            new("Rendimento tributável", Formato.Moeda(tributavel), [rendimento]),
            MemoriaTributariaDetalhada.TabelaIrrf(tabelas),
            MemoriaTributariaDetalhada.BaseIrrf(irrf, r.SessentaECincoAnos ? "valor bruto - parcela isenta" : "valor bruto")
        };
        memoria.AddRange(FaixasIrrf(irrf, r.DecimoTerceiro));
        memoria.Add(new GrupoMemoriaDto(rotuloLiquido, $"Líquido: {Formato.Moeda(liquido)}",
            [new("Valor bruto - IRRF", $"{Formato.Moeda(r.ValorBruto)} - {Formato.Moeda(irrf.Imposto)} = {Formato.Moeda(liquido)}")]));

        var observacoes = new List<string>
        {
            $"IRRF pela tabela da competência {Formato.Competencia(tabelas.Competencia)}. Aposentadoria e pensão pagas pelo INSS ou pela previdência complementar não têm desconto de INSS nem depósito de FGTS.",
            r.SessentaECincoAnos
                ? $"A partir do mês em que o beneficiário completa 65 anos, até {Formato.Moeda(ParcelaIsentaAposentadoria.Valor)} por mês da aposentadoria ou pensão são isentos, além da faixa isenta da tabela (Lei 7.713/1988, art. 6º, XV). A parcela sai da base nas duas modalidades: o desconto simplificado substitui só as demais deduções (Solução de Consulta Cosit 99.004/2026)."
                : $"A partir do mês em que o beneficiário completa 65 anos, até {Formato.Moeda(ParcelaIsentaAposentadoria.Valor)} por mês passam a ser isentos (Lei 7.713/1988, art. 6º, XV). Para simular, escolha \"Sim\" em \"65 anos ou mais\".",
            Vantagem(irrf),
            r.DecimoTerceiro
                ? $"O 13º (abono anual) é tributado exclusivamente na fonte, à parte do benefício do mês, e tem parcela isenta própria de até {Formato.Moeda(ParcelaIsentaAposentadoria.Valor)} aos 65 anos ou mais. A dispensa de retenção de até R$ 10,00 não vale para ele. No INSS, o imposto é retido na parcela paga no fim do ano, sobre o 13º inteiro."
                : "Para o 13º (abono anual), que tem tributação exclusiva e parcela isenta própria, escolha 13º em Rendimento.",
            "Não são consideradas a isenção por moléstia grave (art. 6º, XIV), a pensão alimentícia e outros rendimentos do mês."
        };

        return new DemonstrativoDto("Simulação tributária", $"Competência {Formato.Competencia(tabelas.Competencia)} • {(r.DecimoTerceiro ? "13º da aposentadoria ou pensão" : "aposentadoria ou pensão")}{(r.SessentaECincoAnos ? ", 65 anos ou mais" : "")} • {Dependentes(r)}",
            destaques,
            [new(r.DecimoTerceiro ? "13º da aposentadoria ou pensão" : "Aposentadoria ou pensão", "", r.ValorBruto)],
            [new(MemoriaTributaria.DescricaoIrrf("IRRF", irrf), MemoriaTributaria.ReferenciaIrrf(irrf), irrf.Imposto)],
            [new("Parcela isenta (65 anos ou mais)", "", isenta)], memoria, observacoes,
            RotuloProventos: "Rendimentos", RotuloResultado: rotuloLiquido, Comparativo: comparativo);
    }

    private static string Dependentes(SimularImpostoRequest r) =>
        r.QuantidadeDependentes switch { 0 => "sem dependentes", 1 => "1 dependente", var quantidade => $"{quantidade} dependentes" };

    private static string DescreverIrrf(ApuracaoIrrf irrf) => irrf switch
    {
        { RetencaoDispensada: true } => $"Até {Formato.Moeda(irrf.LimiteDispensa)}: não é retido",
        { ImpostoCalculado: 0m } => "Isento",
        { SimplificadaAplicada: true } => $"Desconto simplificado • efetiva {Formato.Percentual(irrf.Aplicada.AliquotaEfetiva)}",
        _ => $"Deduções legais • efetiva {Formato.Percentual(irrf.Aplicada.AliquotaEfetiva)}"
    };

    private static string Vantagem(ApuracaoIrrf irrf)
    {
        if (irrf.DescontoSimplificado is null)
            return "Antes de 05/2023 não havia o desconto simplificado mensal: o IRRF usa as deduções legais.";
        var normal = irrf.Normal.Imposto;
        var simplificado = irrf.Simplificada.Imposto;
        if (normal == simplificado)
            return normal == 0m ? "Não há IRRF nas duas modalidades." : $"As duas modalidades resultam no mesmo IRRF, de {Formato.Moeda(normal)}.";
        return normal > simplificado
            ? $"O desconto simplificado é mais vantajoso: {Formato.Moeda(normal - simplificado)} a menos de IRRF que as deduções legais. A fonte pagadora aplica a modalidade de menor imposto."
            : $"As deduções legais são mais vantajosas: {Formato.Moeda(simplificado - normal)} a menos de IRRF que o desconto simplificado. A fonte pagadora aplica a modalidade de menor imposto.";
    }

    private static LinhaComparativaDto Linha(string nome, ModalidadeIrrf modalidade, bool aplicada) =>
        new(aplicada ? $"{nome} (aplicada)" : nome,
            [Formato.Moeda(modalidade.BaseCalculo), Formato.Moeda(modalidade.ReducaoMensal), Formato.Moeda(modalidade.Imposto), Formato.Percentual(modalidade.AliquotaEfetiva)], aplicada);

    /// <summary>IRRF faixa a faixa em cada modalidade disponível e a modalidade retida.</summary>
    private static IEnumerable<GrupoMemoriaDto> FaixasIrrf(ApuracaoIrrf irrf, bool tributacaoExclusiva)
    {
        if (MemoriaTributariaDetalhada.FaixasIrrf("IRRF por faixas - deduções legais", irrf.Normal, irrf) is { } normal)
            yield return normal;
        if (irrf.DescontoSimplificado is not null && MemoriaTributariaDetalhada.FaixasIrrf("IRRF por faixas - desconto simplificado", irrf.Simplificada, irrf) is { } simplificada)
            yield return simplificada;
        yield return MemoriaTributariaDetalhada.Retido("IRRF retido", irrf, tributacaoExclusiva);
    }
}
