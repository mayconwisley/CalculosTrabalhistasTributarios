using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Horas de sobreaviso e prontidão por competência e os reflexos da soma, num demonstrativo bruto.</summary>
public sealed class SimularSobreavisoUseCase : ISimularDemonstrativoUseCase<SimularSobreavisoRequest>
{
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularSobreavisoRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultado = CalculadoraSobreaviso.Calcular(request.Entrada);
        return Task.FromResult(resultado.Falhou ? Result<DemonstrativoDto>.Falha(resultado.Erro) : Montar(resultado.Valor));
    }

    private static Result<DemonstrativoDto> Montar(ApuracaoSobreaviso s)
    {
        var r = s.Reflexos;
        var entrada = s.Entrada;
        var reflexos = r.TotalAoEmpregado - r.Principal;
        var meses = s.Meses.Count == 1 ? "1 competência" : $"{s.Meses.Count} competências";
        var temFgts = entrada.Incidencias.HasFlag(IncidenciasReflexo.Fgts);

        var destaques = new List<DestaqueDto>
        {
            new("Total bruto ao empregado", Formato.Moeda(r.TotalAoEmpregado), "Horas e reflexos, antes de INSS e IRRF"),
            new(r.Verba.Descricao, Formato.Moeda(r.Principal), Horas(s)),
            new("DSR e reflexos", Formato.Moeda(reflexos), entrada.Habitual ? "DSR, 13º, férias e aviso, conforme as premissas" : "Horas eventuais: sem reflexos"),
            new("FGTS", temFgts ? Formato.Moeda(r.Fgts + r.MultaFgts) : "Não calculado", r.MultaFgts > 0m ? "8% e a multa de 40%; depositado" : "8%; depositado, fora do total")
        };

        // As horas de sobreaviso e as de prontidão em linhas próprias; os reflexos da soma vêm do motor.
        var proventos = new List<VerbaDto>();
        if (s.TotalSobreaviso > 0m) proventos.Add(new("Horas de sobreaviso (1/3)", Formato.Horas(s.HorasSobreaviso), s.TotalSobreaviso));
        if (s.TotalProntidao > 0m) proventos.Add(new("Horas de prontidão (2/3)", Formato.Horas(s.HorasProntidao), s.TotalProntidao));
        proventos.AddRange(MemoriaReflexos.Proventos(r).Skip(1));

        var formulas = s.Meses.Select(mes => new FormulaDto($"Horas de {Formato.Competencia(mes.Mes.Competencia)}", FormulaDoMes(mes, entrada.Divisor))).ToArray();
        var memoria = new List<GrupoMemoriaDto>
        {
            new("Horas por competência", $"{meses}: {Formato.Moeda(r.Principal)}", formulas),
            MemoriaReflexos.Grupo(r)
        };

        var observacoes = new List<string>
        {
            "Sobreaviso: o empregado fica fora da empresa aguardando o chamado, com a liberdade restringida pelo empregador, inclusive por celular ou outro meio telemático em escala de plantão; o simples uso do celular da empresa não basta (Súmula 428 do TST). As horas são pagas à razão de 1/3 do salário-hora normal, em escalas de até 24 horas (CLT, art. 244, § 2º, aplicado por analogia).",
            "Prontidão: o empregado aguarda ordens nas dependências da empresa, à razão de 2/3 do salário-hora normal, em escalas de até 12 horas (CLT, art. 244, § 3º). Informe as horas totais do mês; o limite de cada escala não é conferido aqui.",
            "As horas em que o empregado foi chamado e trabalhou não são de sobreaviso: são horas de trabalho, normais ou extras, e devem ficar fora destas linhas. Use Horas extras e adicionais para elas.",
            "O salário-hora é o salário normal do mês ÷ divisor. O adicional de periculosidade não integra as horas de sobreaviso (Súmula 132, II, do TST); para os eletricitários, a base é a totalidade das parcelas salariais (Súmula 229 do TST): some-as no salário do mês.",
            entrada.Habitual
                ? "Os reflexos são premissas da simulação: horas pagas com habitualidade integram a remuneração e refletem no DSR, no 13º, nas férias com o terço e, na dispensa, no aviso prévio indenizado. Convenções coletivas podem prever outro percentual ou outra forma de pagamento."
                : "Horas eventuais não integram a remuneração para o DSR, o 13º, as férias e o aviso; o FGTS incide sobre o valor pago no mês.",
            "Demonstrativo bruto: INSS, IRRF, atualização monetária e juros não estão incluídos. O FGTS e a multa são depositados pelo empregador e não somam ao total."
        };
        if (MemoriaReflexos.ObservacaoFerias(r) is { } ferias)
            observacoes.Add(ferias);

        var periodo = s.Meses.Count == 1 ? Formato.Competencia(s.Meses[0].Mes.Competencia) : $"{Formato.Competencia(s.Meses[0].Mes.Competencia)} a {Formato.Competencia(s.Meses[^1].Mes.Competencia)}";
        return new DemonstrativoDto("Sobreaviso e prontidão", $"Competências {periodo} • divisor {Formato.Numero(entrada.Divisor)}",
            destaques, proventos, [], MemoriaReflexos.Informativos(r), memoria, observacoes,
            RotuloProventos: "Horas e reflexos", RotuloResultado: "Total bruto ao empregado");
    }

    private static string Horas(ApuracaoSobreaviso s) => (s.HorasSobreaviso > 0m, s.HorasProntidao > 0m) switch
    {
        (true, true) => $"{Formato.Horas(s.HorasSobreaviso)} de sobreaviso e {Formato.Horas(s.HorasProntidao)} de prontidão",
        (false, true) => $"{Formato.Horas(s.HorasProntidao)} a 2/3 do salário-hora",
        _ => $"{Formato.Horas(s.HorasSobreaviso)} a 1/3 do salário-hora"
    };

    private static string FormulaDoMes(SobreavisoDoMes mes, decimal divisor)
    {
        var hora = $"{Formato.Moeda(mes.Mes.Salario)} ÷ {Formato.Numero(divisor)}";
        var partes = new List<string>();
        if (mes.Mes.HorasSobreaviso > 0m) partes.Add($"sobreaviso: {hora} x {Formato.Horas(mes.Mes.HorasSobreaviso)} x 1/3 = {Formato.Moeda(mes.ValorSobreaviso)}");
        if (mes.Mes.HorasProntidao > 0m) partes.Add($"prontidão: {hora} x {Formato.Horas(mes.Mes.HorasProntidao)} x 2/3 = {Formato.Moeda(mes.ValorProntidao)}");
        return partes.Count == 0 ? "Sem horas no mês: R$ 0,00" : string.Join("; ", partes);
    }
}
