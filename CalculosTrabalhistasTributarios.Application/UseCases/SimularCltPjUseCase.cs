using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Compara, em um ano, o mesmo profissional como empregado (CLT) e como PJ no Simples Nacional: o que sobra para ele e quanto
/// custa para a empresa. Quando o valor do PJ não é informado, calcula o valor mensal que iguala o total do CLT.
/// </summary>
public sealed class SimularCltPjUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularCltPjRequest>
{
    private const decimal AliquotaPatronal = 20m;
    private const decimal Rat = 2m;
    private const decimal Terceiros = 5.8m;
    private const decimal AliquotaFgts = 8m;
    private const decimal LimiteLucroIsento = 50_000m;

    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularCltPjRequest r, CancellationToken cancellationToken)
    {
        if (r.SalarioClt <= 0m)
            return Erro.Validacao("Informe o salário mensal como CLT.");
        if (r.Dependentes < 0 || r.BeneficiosClt < 0m || r.ValorPj < 0m || r.CustosPj < 0m)
            return Erro.Validacao("Os valores e a quantidade de dependentes não podem ser negativos.");
        if (r.ValorPj * 12m > SimplesNacional.LimiteReceitaAnual)
            return Erro.Validacao("O valor do PJ passa do limite do Simples Nacional, de R$ 4,8 milhões por ano (R$ 400.000,00 por mês).");

        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(r.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        var consultaSalarioMinimo = tabelas.ObterSalarioMinimo();
        if (consultaSalarioMinimo.Falhou)
            return consultaSalarioMinimo.Erro;
        var salarioMinimo = consultaSalarioMinimo.Valor;
        var clt = Clt(tabelas, r);
        var equivalente = ValorPjEquivalente(tabelas, r, salarioMinimo, clt.Total);
        if (r.ValorPj <= 0m && equivalente is null)
            return Erro.Validacao("Nenhum valor de PJ dentro do limite do Simples Nacional iguala o total do CLT.");
        var valorPj = r.ValorPj > 0m ? r.ValorPj : equivalente!.Value;
        var pj = Pj(tabelas, r, salarioMinimo, valorPj);
        var diferenca = pj.Total - clt.Total;

        var linhas = new List<LinhaComparativaDto>
        {
            Linha("Recebido no ano (bruto)", clt.Bruto, pj.Faturamento),
            Linha("INSS", clt.Inss, pj.Inss),
            Linha("IRRF", clt.Irrf, pj.Irrf),
            Linha("Simples Nacional (DAS)", 0m, pj.Das),
            Linha("Custos do PJ", 0m, pj.Custos),
            Linha("Líquido no ano", clt.Liquido, pj.Liquido),
            Linha("FGTS depositado", clt.Fgts, 0m),
            Linha("Benefícios", clt.Beneficios, 0m),
            Linha("Total para o profissional", clt.Total, pj.Total, destaque: true),
            Linha("Média por mês", Arredondar(clt.Total / 12m), Arredondar(pj.Total / 12m)),
            Linha("Custo para a empresa no ano", clt.CustoEmpresa, pj.Faturamento, destaque: true)
        };

        var observacoes = new List<string>
        {
            "O CLT considera 12 salários, o 13º e as férias com 1/3, sem horas extras nem PLR; o total soma o líquido, o FGTS depositado e os benefícios.",
            "O PJ considera faturamento igual todo mês, no Simples Nacional, com o pró-labore descontado de INSS (11%) e IRRF e o restante distribuído como lucro, isento de IR. O ISS e a contribuição patronal sobre o pró-labore estão no DAS.",
            "Como PJ não há FGTS, 13º, férias remuneradas, seguro-desemprego nem multa do FGTS na saída; o profissional também perde a proteção da CLT.",
            CustoEmpresa(r.RegimeEmpresa)
        };
        if (pj.LucroMensal > LimiteLucroIsento)
            observacoes.Insert(2, $"O lucro distribuído passa de {Formato.Moeda(LimiteLucroIsento)} por mês: a partir de 2026, ele tem retenção de IRRF de 10% (Lei 15.270/2025), que não está neste cálculo.");

        return new DemonstrativoDto(
            "Comparação CLT x PJ",
            $"Competência {Formato.Competencia(tabelas.Competencia)} • Salário CLT de {Formato.Moeda(r.SalarioClt)}",
            [
                new("CLT: total no ano", Formato.Moeda(clt.Total), "Líquido, FGTS e benefícios"),
                new("PJ: total no ano", Formato.Moeda(pj.Total), $"Nota de {Formato.Moeda(valorPj)} por mês"),
                new("Diferença no ano", Formato.MoedaComSinal(diferenca), diferenca > 0m ? "A favor do PJ" : diferenca < 0m ? "A favor do CLT" : "Os dois resultam no mesmo"),
                new("PJ equivalente", equivalente is { } valor ? Formato.Moeda(valor) : "Acima do Simples", "Por mês, para igualar o total do CLT")
            ],
            [],
            [],
            [],
            [MemoriaClt(r, clt), MemoriaPj(r, pj, valorPj), MemoriaEquivalencia(clt.Total, equivalente)],
            observacoes,
            Comparativo: new TabelaComparativaDto("Comparação no ano", ["CLT", "PJ", "Diferença"], linhas));
    }

    private sealed record ResultadoClt(decimal Salario, decimal Ferias, decimal Bruto, decimal InssMensal, decimal IrrfMensal, decimal InssFerias, decimal IrrfFerias, decimal Inss13, decimal Irrf13,
        decimal Fgts, decimal Beneficios, decimal AliquotaEncargos, decimal CustoEmpresa)
    {
        public decimal Inss => InssMensal * 11m + InssFerias + Inss13;
        public decimal Irrf => IrrfMensal * 11m + IrrfFerias + Irrf13;
        public decimal Liquido => Bruto - Inss - Irrf;
        public decimal Total => Liquido + Fgts + Beneficios;
    }

    private sealed record ResultadoPj(decimal ValorMensal, AliquotaSimples Simples, decimal DasMensal, decimal ProLabore, decimal InssMensal, decimal IrrfMensal, decimal CustosMensais)
    {
        public decimal Faturamento => ValorMensal * 12m;
        public decimal Das => DasMensal * 12m;
        public decimal Inss => InssMensal * 12m;
        public decimal Irrf => IrrfMensal * 12m;
        public decimal Custos => CustosMensais * 12m;
        public decimal LucroMensal => ValorMensal - DasMensal - ProLabore - CustosMensais;
        public decimal Liquido => Faturamento - Das - Inss - Irrf - Custos;
        public decimal Total => Liquido;
    }

    /// <summary>
    /// Um ano de CLT: 11 meses de salário, o mês de férias com 1/3 (INSS e IRRF à parte) e o 13º (IRRF exclusivo). O FGTS é
    /// de 8% sobre tudo, e a empresa paga também os encargos do regime e os benefícios.
    /// </summary>
    private static ResultadoClt Clt(TabelasDaCompetencia tabelas, SimularCltPjRequest r)
    {
        var salario = r.SalarioClt;
        var ferias = Arredondar(salario * 4m / 3m);
        var inssMensal = tabelas.CalcularInss(salario).Valor;
        var inssFerias = tabelas.CalcularInss(ferias).Valor;
        var inss13 = tabelas.CalcularInss(salario).Valor;
        var bruto = salario * 11m + ferias + salario;
        var aliquotaEncargos = r.RegimeEmpresa switch
        {
            RegimeTributario.LucroRealOuPresumido => AliquotaPatronal + Rat + Terceiros + AliquotaFgts,
            RegimeTributario.SimplesNacionalAnexoIV => AliquotaPatronal + Rat + AliquotaFgts,
            _ => AliquotaFgts
        };
        return new ResultadoClt(salario, ferias, bruto,
            inssMensal, tabelas.CalcularIrrf(salario, inssMensal, r.Dependentes).Imposto,
            inssFerias, tabelas.CalcularIrrf(ferias, inssFerias, r.Dependentes).Imposto,
            inss13, tabelas.CalcularIrrf(salario, inss13, r.Dependentes, tributacaoExclusiva: true).Imposto,
            Arredondar(bruto * AliquotaFgts / 100m), r.BeneficiosClt * 12m, aliquotaEncargos,
            Arredondar(bruto * (100m + aliquotaEncargos) / 100m) + r.BeneficiosClt * 12m);
    }

    /// <summary>
    /// Um ano de PJ: o DAS pela alíquota efetiva da receita de 12 meses; o pró-labore de 28% do faturamento no fator R (no
    /// mínimo um salário mínimo) ou de um salário mínimo, com INSS de 11% e IRRF; e os custos da empresa.
    /// </summary>
    private static ResultadoPj Pj(TabelasDaCompetencia tabelas, SimularCltPjRequest r, decimal salarioMinimo, decimal valorMensal)
    {
        var anexo = r.Tributacao == TributacaoPj.AnexoV ? AnexoSimples.V : AnexoSimples.III;
        // A receita chega aqui dentro do limite do Simples: o valor informado é validado antes, e a busca do equivalente para no limite.
        var simples = SimplesNacional.Aliquota(anexo, valorMensal * 12m).Valor;
        var das = Arredondar(valorMensal * simples.AliquotaEfetiva / 100m);
        // No fator R, o pró-labore é arredondado para cima, para a folha não ficar abaixo dos 28%.
        var proLabore = Math.Min(valorMensal, r.Tributacao == TributacaoPj.AnexoIIIFatorR
            ? Math.Max(salarioMinimo, Math.Ceiling(valorMensal * SimplesNacional.FatorRMinimo) / 100m)
            : salarioMinimo);
        var inss = tabelas.CalcularInssContribuinteIndividual(proLabore).Valor;
        return new ResultadoPj(valorMensal, simples, das, proLabore, inss, tabelas.CalcularIrrf(proLabore, inss, r.Dependentes).Imposto, r.CustosPj);
    }

    /// <summary>Menor valor mensal de PJ, em centavos, cujo total no ano alcança o do CLT; nulo se passar do limite do Simples.</summary>
    private static decimal? ValorPjEquivalente(TabelasDaCompetencia tabelas, SimularCltPjRequest r, decimal salarioMinimo, decimal totalClt)
    {
        decimal Total(long centavos) => Pj(tabelas, r, salarioMinimo, centavos / 100m).Total;
        var minimo = 0L;
        var maximo = (long)(SimplesNacional.LimiteReceitaAnual / 12m * 100m);
        if (Total(maximo) < totalClt)
            return null;
        while (minimo < maximo)
        {
            var meio = minimo + (maximo - minimo) / 2;
            if (Total(meio) >= totalClt) maximo = meio; else minimo = meio + 1;
        }
        return minimo / 100m;
    }

    private static GrupoMemoriaDto MemoriaClt(SimularCltPjRequest r, ResultadoClt clt) => new("CLT", $"Total: {Formato.Moeda(clt.Total)}",
    [
        new("Recebido no ano", $"{Formato.Moeda(clt.Salario)} x 11 meses + {Formato.Moeda(clt.Ferias)} (mês de férias com 1/3) + {Formato.Moeda(clt.Salario)} (13º) = {Formato.Moeda(clt.Bruto)}"),
        new("INSS", $"{Formato.Moeda(clt.InssMensal)} x 11 + {Formato.Moeda(clt.InssFerias)} (férias) + {Formato.Moeda(clt.Inss13)} (13º) = {Formato.Moeda(clt.Inss)}"),
        new("IRRF", $"{Formato.Moeda(clt.IrrfMensal)} x 11 + {Formato.Moeda(clt.IrrfFerias)} (férias) + {Formato.Moeda(clt.Irrf13)} (13º, tributação exclusiva) = {Formato.Moeda(clt.Irrf)}"),
        new("Líquido", $"{Formato.Moeda(clt.Bruto)} - {Formato.Moeda(clt.Inss)} - {Formato.Moeda(clt.Irrf)} = {Formato.Moeda(clt.Liquido)}"),
        new("FGTS", $"{Formato.Moeda(clt.Bruto)} x 8% = {Formato.Moeda(clt.Fgts)}, depositado na conta do FGTS"),
        new("Benefícios", $"{Formato.Moeda(r.BeneficiosClt)} x 12 = {Formato.Moeda(clt.Beneficios)}"),
        new("Custo para a empresa", $"{Formato.Moeda(clt.Bruto)} x (100% + {Formato.Percentual(clt.AliquotaEncargos)} de encargos e FGTS) + {Formato.Moeda(clt.Beneficios)} (benefícios) = {Formato.Moeda(clt.CustoEmpresa)}")
    ]);

    private static GrupoMemoriaDto MemoriaPj(SimularCltPjRequest r, ResultadoPj pj, decimal valorPj)
    {
        var simples = pj.Simples;
        var formulas = new List<FormulaDto>
        {
            new("Faturamento", $"{Formato.Moeda(valorPj)} x 12 = {Formato.Moeda(pj.Faturamento)}{(r.ValorPj == 0m ? ", o valor que iguala o total do CLT" : "")}"),
            new("Anexo do Simples", r.Tributacao switch
            {
                TributacaoPj.AnexoIIIFatorR => $"Anexo III pelo fator R: o pró-labore de {Formato.Moeda(pj.ProLabore)} é pelo menos 28% do faturamento",
                TributacaoPj.AnexoIII => "Anexo III: a atividade já é tributada nesse anexo",
                _ => "Anexo V: a folha fica abaixo de 28% do faturamento"
            }),
            new("Alíquota efetiva", simples.Faixa == 1
                ? $"Faixa 1 do anexo {simples.Anexo}: {Formato.Percentual(simples.AliquotaNominal)}"
                : $"Faixa {simples.Faixa} do anexo {simples.Anexo}: ({Formato.Moeda(pj.Faturamento)} x {Formato.Percentual(simples.AliquotaNominal)} - {Formato.Moeda(simples.ParcelaDeduzir)}) ÷ {Formato.Moeda(pj.Faturamento)} = {Formato.Percentual(Math.Round(simples.AliquotaEfetiva, 4))}"),
            new("DAS", $"{Formato.Moeda(valorPj)} x {Formato.Percentual(Math.Round(simples.AliquotaEfetiva, 4))} = {Formato.Moeda(pj.DasMensal)} por mês; {Formato.Moeda(pj.Das)} no ano"),
            new("Pró-labore", $"{Formato.Moeda(pj.ProLabore)} por mês: INSS de {Formato.Moeda(pj.InssMensal)} (11%) e IRRF de {Formato.Moeda(pj.IrrfMensal)}"),
            new("Lucro distribuído", $"{Formato.Moeda(valorPj)} - {Formato.Moeda(pj.DasMensal)} (DAS) - {Formato.Moeda(pj.ProLabore)} (pró-labore){(pj.CustosMensais > 0m ? $" - {Formato.Moeda(pj.CustosMensais)} (custos)" : "")} = {Formato.Moeda(pj.LucroMensal)} por mês, isento de IR"),
            new("Líquido", $"{Formato.Moeda(pj.Faturamento)} - {Formato.Moeda(pj.Das)} (DAS) - {Formato.Moeda(pj.Inss)} (INSS) - {Formato.Moeda(pj.Irrf)} (IRRF){(pj.Custos > 0m ? $" - {Formato.Moeda(pj.Custos)} (custos)" : "")} = {Formato.Moeda(pj.Liquido)}")
        };
        return new GrupoMemoriaDto("PJ", $"Total: {Formato.Moeda(pj.Total)}", formulas);
    }

    private static GrupoMemoriaDto MemoriaEquivalencia(decimal totalClt, decimal? equivalente) => new("PJ equivalente", equivalente is { } valor ? $"{Formato.Moeda(valor)} por mês" : "Acima do Simples",
    [
        new("Equivalência", equivalente is { } valorMensal
            ? $"Com uma nota de {Formato.Moeda(valorMensal)} por mês, o PJ chega ao total de {Formato.Moeda(totalClt)} no ano do CLT, nas mesmas condições."
            : $"Nenhum valor dentro do limite do Simples Nacional chega ao total de {Formato.Moeda(totalClt)} do CLT.")
    ]);

    private static string CustoEmpresa(RegimeTributario regime) => regime switch
    {
        RegimeTributario.LucroRealOuPresumido => "O custo do CLT para a empresa no Lucro Real ou Presumido inclui 20% de INSS patronal, RAT de 2% (FAP 1), 5,8% de terceiros e 8% de FGTS sobre o recebido no ano; o do PJ é o valor das notas.",
        RegimeTributario.SimplesNacionalAnexoIV => "O custo do CLT para a empresa no Simples, anexo IV, inclui 20% de INSS patronal, RAT de 2% e 8% de FGTS sobre o recebido no ano; o do PJ é o valor das notas.",
        _ => "O custo do CLT para a empresa no Simples (anexos I a III e V) inclui só o FGTS de 8%, porque a contribuição patronal está no DAS; o do PJ é o valor das notas."
    };

    private static LinhaComparativaDto Linha(string descricao, decimal clt, decimal pj, bool destaque = false) =>
        new(descricao, [Formato.Moeda(clt), Formato.Moeda(pj), Formato.MoedaComSinal(pj - clt)], destaque);

    private static decimal Arredondar(decimal valor) => CalculadoraTributacao.Arredondar(valor);
}
