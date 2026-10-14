using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Gorjetas;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Gorjetas repassadas por competência, a retenção da empresa, os reflexos e a média de 12 meses.</summary>
public sealed class SimularGorjetasUseCase : ISimularDemonstrativoUseCase<SimularGorjetasRequest>
{
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularGorjetasRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultado = CalculadoraGorjetas.Calcular(request.Entrada);
        return Task.FromResult(resultado.Falhou ? Result<DemonstrativoDto>.Falha(resultado.Erro) : Montar(resultado.Valor));
    }

    private static Result<DemonstrativoDto> Montar(ApuracaoGorjetas g)
    {
        var r = g.Reflexos;
        var entrada = g.Entrada;
        var retencao = Formato.PercentualCurto(entrada.PercentualRetencao);
        var meses = g.Meses.Count == 1 ? "1 competência" : $"{g.Meses.Count} competências";
        var temFgts = entrada.Incidencias.HasFlag(IncidenciasReflexo.Fgts);

        var destaques = new List<DestaqueDto>
        {
            new("Total ao empregado", Formato.Moeda(r.TotalAoEmpregado), "Gorjetas e reflexos, antes de INSS e IRRF"),
            new("Gorjetas repassadas", Formato.Moeda(g.Repassado), g.Retido > 0m ? $"{Formato.Moeda(g.Arrecadado)} menos {retencao} retidos" : "Sem retenção pela empresa"),
            new("Média mensal", Formato.Moeda(g.Media), g.MesesMedia == 1 ? "Do único mês informado" : $"Dos últimos {g.MesesMedia} meses"),
            new("FGTS", temFgts ? Formato.Moeda(r.Fgts + r.MultaFgts) : "Não calculado", r.MultaFgts > 0m ? "8% e a multa de 40%; depositado" : "8%; depositado, fora do total")
        };

        var proventos = new List<VerbaDto> { new("Gorjetas repassadas", meses, g.Repassado) };
        proventos.AddRange(MemoriaReflexos.Proventos(r).Skip(1));
        var informativos = new List<VerbaDto>();
        if (g.Retido > 0m)
            informativos.Add(new("Retenção da empresa para os encargos", retencao, g.Retido));
        informativos.AddRange(MemoriaReflexos.Informativos(r));

        var formulas = g.Meses.Select(mes => new FormulaDto($"Gorjetas de {Formato.Competencia(mes.Mes.Competencia)}", mes.Retencao > 0m
            ? $"{Formato.Moeda(mes.Mes.Valor)} - {retencao} ({Formato.Moeda(mes.Retencao)}) = {Formato.Moeda(mes.Repasse)}"
            : $"{Formato.Moeda(mes.Repasse)}, sem retenção")).ToList();
        formulas.Add(new("Média mensal", $"{Formato.Moeda(g.Meses.TakeLast(g.MesesMedia).Sum(mes => mes.Repasse))} ÷ {g.MesesMedia} = {Formato.Moeda(g.Media)}"));
        var memoria = new List<GrupoMemoriaDto>
        {
            new("Gorjetas por competência", $"{meses}: {Formato.Moeda(g.Repassado)}", formulas),
            MemoriaReflexos.Grupo(r)
        };

        var observacoes = new List<string>
        {
            "A gorjeta, cobrada na nota como taxa de serviço ou dada espontaneamente pelo cliente, não é receita da empresa e integra a remuneração do empregado (CLT, art. 457, §§ 3º e 4º). Informe em cada mês a parte do empregado no rateio, conforme a convenção ou o acordo coletivo ou, na falta deles, a assembleia dos trabalhadores (§ 5º).",
            $"Com previsão em convenção ou acordo coletivo, a empresa pode reter até 20% da arrecadação, se estiver no Simples Nacional, ou até 33%, nos demais regimes, para custear os encargos sociais, previdenciários e trabalhistas; o restante é repassado integralmente ao empregado (§ 6º). Limite deste cálculo: {Formato.PercentualCurto(g.LimiteRetencao)}.",
            "As gorjetas repassadas refletem no 13º, nas férias com o terço e no FGTS, mas não são base do aviso prévio, do adicional noturno, das horas extras nem do repouso semanal remunerado (Súmula 354 do TST).",
            "Média mensal: se a empresa cobrava a gorjeta havia mais de 12 meses e deixa de cobrá-la, a média dos últimos 12 meses é incorporada ao salário, salvo o que dispuser a norma coletiva (§ 9º). A média mostrada usa os últimos 12 meses informados.",
            "A CTPS e o contracheque devem trazer o salário fixo e o percentual ou o valor das gorjetas (§ 8º).",
            "Demonstrativo bruto: INSS, IRRF, atualização monetária e juros não estão incluídos. O FGTS e a multa são depositados pelo empregador e não somam ao total."
        };
        if (MemoriaReflexos.ObservacaoFerias(r) is { } ferias)
            observacoes.Add(ferias);

        var periodo = g.Meses.Count == 1 ? Formato.Competencia(g.Meses[0].Mes.Competencia) : $"{Formato.Competencia(g.Meses[0].Mes.Competencia)} a {Formato.Competencia(g.Meses[^1].Mes.Competencia)}";
        return new DemonstrativoDto("Gorjetas", $"Competências {periodo} • retenção de {retencao}",
            destaques, proventos, [], informativos, memoria, observacoes,
            RotuloProventos: "Gorjetas e reflexos", RotuloResultado: "Total ao empregado");
    }
}
