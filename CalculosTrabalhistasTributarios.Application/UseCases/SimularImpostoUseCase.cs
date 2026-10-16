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
/// bruto, salário líquido e o FGTS do tipo de vínculo, que é depositado pelo empregador e não reduz o líquido.
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
        var inss = tabelas.CalcularInss(request.BaseInss);
        var irrf = tabelas.CalcularIrrf(request.ValorBruto, inss.Valor, request.QuantidadeDependentes);
        return Montar(request, tabelas, inss, irrf);
    }

    private static Result Validar(SimularImpostoRequest r)
    {
        if (r.ValorBruto < 0m || r.ValorBruto > LimiteValor)
            return Erro.Validacao($"Valor bruto: informe um valor de zero a {Formato.Moeda(LimiteValor)}, por exemplo 3.500,00.");
        if (r.BaseInss < 0m || r.BaseInss > LimiteValor)
            return Erro.Validacao($"Base de INSS: informe um valor de zero a {Formato.Moeda(LimiteValor)}; em geral, é igual ao valor bruto.");
        if (r.QuantidadeDependentes is < 0 or > LimiteDependentes)
            return Erro.Validacao($"Dependentes: informe de 0 a {LimiteDependentes} dependentes para a dedução do IRRF.");
        if (!Enum.IsDefined(r.Vinculo))
            return Erro.Validacao("Tipo de vínculo: escolha empregado, aprendiz ou doméstico.");
        return Result.Ok();
    }

    private static DemonstrativoDto Montar(SimularImpostoRequest r, TabelasDaCompetencia tabelas, ApuracaoInss inss, ApuracaoIrrf irrf)
    {
        var liquido = r.ValorBruto - inss.Valor - irrf.Imposto;
        // O FGTS não tem teto: incide sobre toda a remuneração informada, e não sobre a base limitada do INSS.
        var aliquotaFgts = ConferenciaFgts.Aliquota(r.Vinculo);
        var fgts = CalculadoraTributacao.Arredondar(r.BaseInss * aliquotaFgts / 100m);
        var domestico = r.Vinculo == CategoriaFgts.Domestico;
        var compensatoria = domestico ? CalculadoraTributacao.Arredondar(r.BaseInss * AliquotaCompensatoria / 100m) : 0m;
        var simplificadoDisponivel = irrf.DescontoSimplificado is not null;

        var destaques = new List<DestaqueDto>
        {
            new("Salário líquido", Formato.Moeda(liquido), irrf.RetencaoDispensada ? $"IRRF de {Formato.Moeda(irrf.ImpostoCalculado)} dispensado" : "Valor bruto menos INSS e IRRF"),
            new("INSS", Formato.Moeda(inss.Valor), inss.LimitadaAoTeto ? $"Base limitada ao teto de {Formato.Moeda(inss.BaseConsiderada)}" : $"Base: {Formato.Moeda(inss.BaseConsiderada)}"),
            new("IRRF", Formato.Moeda(irrf.Imposto), DescreverIrrf(irrf)),
            domestico
                ? new("FGTS e indenização", Formato.Moeda(fgts + compensatoria), $"8% ({Formato.Moeda(fgts)}) + 3,2% ({Formato.Moeda(compensatoria)})")
                : new($"FGTS ({Formato.PercentualCurto(aliquotaFgts)})", Formato.Moeda(fgts), r.Vinculo == CategoriaFgts.Aprendiz ? "Contrato de aprendizagem" : "Depositado pelo empregador")
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
            MemoriaTributaria.Inss("INSS", inss, "base de INSS"),
            MemoriaTributaria.Irrf("IRRF", irrf, "valor bruto"),
            Faixas("IRRF por faixas - deduções legais", irrf.Normal)
        };
        if (simplificadoDisponivel)
            memoria.Add(Faixas("IRRF por faixas - desconto simplificado", irrf.Simplificada));
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
                CategoriaFgts.Aprendiz => "Contrato de aprendizagem: FGTS de 2% da remuneração (Lei 8.036/1990, art. 15, § 7º). INSS e IRRF seguem as mesmas tabelas do empregado.",
                CategoriaFgts.Domestico => "Empregado doméstico: o empregador deposita 8% de FGTS e mais 3,2% de indenização compensatória da perda do emprego, que o trabalhador saca na dispensa sem justa causa (LC 150/2015, arts. 22 e 34). Os encargos do empregador estão na calculadora Empregado doméstico (DAE).",
                _ => "FGTS de 8% da remuneração (Lei 8.036/1990, art. 15)."
            } + " O FGTS é depositado pelo empregador, não tem teto e não reduz o salário líquido."
        };
        if (r.BaseInss != r.ValorBruto)
            observacoes.Add($"A base de INSS ({Formato.Moeda(r.BaseInss)}) difere do valor bruto ({Formato.Moeda(r.ValorBruto)}): o INSS e o FGTS incidem sobre a base de INSS, e o IRRF, sobre o valor bruto.");
        observacoes.Add("Não considera pensão alimentícia, previdência complementar, outros descontos nem outros rendimentos do mês. Para o holerite completo, use a calculadora Holerite do mês.");

        var vinculo = r.Vinculo switch { CategoriaFgts.Aprendiz => "aprendiz", CategoriaFgts.Domestico => "doméstico", _ => "empregado" };
        var dependentes = r.QuantidadeDependentes switch { 0 => "sem dependentes", 1 => "1 dependente", var quantidade => $"{quantidade} dependentes" };
        return new DemonstrativoDto("Simulação tributária", $"Competência {Formato.Competencia(tabelas.Competencia)} • {vinculo} • {dependentes}",
            destaques,
            [new("Valor bruto", "", r.ValorBruto)],
            [
                new("INSS", MemoriaTributaria.ReferenciaInss(inss), inss.Valor),
                new(MemoriaTributaria.DescricaoIrrf("IRRF", irrf), MemoriaTributaria.ReferenciaIrrf(irrf), irrf.Imposto)
            ],
            informativos, memoria, observacoes,
            RotuloProventos: "Rendimentos", RotuloResultado: "Salário líquido", Comparativo: comparativo);
    }

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

    /// <summary>
    /// Imposto de cada faixa da tabela progressiva, para conferir o cálculo pela parcela a deduzir. As faixas são arredondadas
    /// uma a uma e a soma pode diferir em centavos do imposto calculado pela parcela a deduzir, que é o que vale.
    /// </summary>
    private static GrupoMemoriaDto Faixas(string titulo, ModalidadeIrrf modalidade)
    {
        if (modalidade.DetalhesProgressivos.Count == 0)
            return new GrupoMemoriaDto(titulo, Formato.Moeda(0m), [new("Total", $"Sem base de cálculo: {Formato.Moeda(0m)}")]);
        var formulas = modalidade.DetalhesProgressivos
            .Select(faixa => new FormulaDto($"Faixa {faixa.Faixa}", $"{Formato.Moeda(faixa.BaseCalculada)} x {Formato.Percentual(faixa.Aliquota)} = {Formato.Moeda(faixa.Imposto)}"))
            .ToList();
        var soma = modalidade.DetalhesProgressivos.Sum(faixa => faixa.Imposto);
        var antesReducao = modalidade.ReducaoMensal > 0m ? ", antes da redução mensal" : "";
        formulas.Add(new("Total", soma == modalidade.ImpostoAntesReducao
            ? $"{string.Join(" + ", modalidade.DetalhesProgressivos.Select(faixa => Formato.Moeda(faixa.Imposto)))} = {Formato.Moeda(soma)}{antesReducao}"
            : $"A soma das faixas é {Formato.Moeda(soma)}; o imposto pela parcela a deduzir é {Formato.Moeda(modalidade.ImpostoAntesReducao)}{antesReducao}. A diferença vem do arredondamento de cada faixa, e vale o da parcela a deduzir."));
        return new GrupoMemoriaDto(titulo, Formato.Moeda(modalidade.ImpostoAntesReducao), formulas);
    }
}
