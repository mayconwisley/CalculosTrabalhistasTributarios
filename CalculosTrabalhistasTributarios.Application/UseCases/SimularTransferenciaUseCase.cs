using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Transferencia;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Adicional de transferência provisória por competência e os seus reflexos, num demonstrativo bruto.</summary>
public sealed class SimularTransferenciaUseCase : ISimularDemonstrativoUseCase<SimularTransferenciaRequest>
{
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularTransferenciaRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultado = CalculadoraTransferencia.Calcular(request.Entrada);
        return Task.FromResult(resultado.Falhou ? Result<DemonstrativoDto>.Falha(resultado.Erro) : Montar(resultado.Valor));
    }

    private static Result<DemonstrativoDto> Montar(ApuracaoTransferencia t)
    {
        var r = t.Reflexos;
        var entrada = t.Entrada;
        var percentual = Formato.PercentualCurto(entrada.Percentual);
        var meses = t.Meses.Count == 1 ? "1 competência" : $"{t.Meses.Count} competências";
        var temFgts = entrada.Incidencias.HasFlag(IncidenciasReflexo.Fgts);

        var destaques = new List<DestaqueDto>
        {
            new("Total ao empregado", Formato.Moeda(t.TotalAoEmpregado), entrada.Despesas > 0m ? "Adicional, reflexos e despesas, antes de INSS e IRRF" : "Adicional e reflexos, antes de INSS e IRRF"),
            new("Adicional de transferência", Formato.Moeda(r.Principal), $"{percentual} do salário em {meses}"),
            new("Reflexos", Formato.Moeda(r.TotalAoEmpregado - r.Principal), r.AvisoPrevio > 0m ? "13º, férias + 1/3 e aviso prévio" : "13º e férias + 1/3"),
            new("FGTS", temFgts ? Formato.Moeda(r.Fgts + r.MultaFgts) : "Não calculado", r.MultaFgts > 0m ? "8% e a multa de 40%; depositado" : "8%; depositado, fora do total")
        };

        // O adicional com o percentual na descrição; os reflexos vêm do motor.
        var proventos = new List<VerbaDto> { new($"Adicional de transferência ({percentual})", meses, r.Principal) };
        proventos.AddRange(MemoriaReflexos.Proventos(r).Skip(1));
        if (entrada.Despesas > 0m)
            proventos.Add(new("Despesas da transferência (CLT, art. 470)", "Reembolso, sem incidências", entrada.Despesas));

        var formulas = t.Meses.Select(mes => new FormulaDto($"Adicional de {Formato.Competencia(mes.Mes.Competencia)}",
            $"{Formato.Moeda(mes.Mes.Salario)} x {percentual} x {mes.Mes.Dias}/30 dias = {Formato.Moeda(mes.Valor)}")).ToArray();
        var memoria = new List<GrupoMemoriaDto>
        {
            new("Adicional por competência", $"{meses}: {Formato.Moeda(r.Principal)}", formulas),
            MemoriaReflexos.Grupo(r)
        };

        var observacoes = new List<string>
        {
            "Na transferência provisória para localidade que exija mudança de domicílio, o empregador paga um adicional de no mínimo 25% dos salários que o empregado recebia na origem, enquanto durar a transferência (CLT, art. 469, § 3º). Informe um percentual maior quando a norma coletiva ou o contrato o previr.",
            "A transferência definitiva não gera o adicional. O cargo de confiança ou a cláusula de transferência no contrato não afastam o direito: o que importa é a transferência ser provisória (OJ 113 da SBDI-1 do TST).",
            "Por mês: salário da origem × percentual × dias transferido ÷ 30. O adicional tem natureza salarial: integra o 13º, as férias com o terço e o FGTS enquanto pago e, na dispensa durante a transferência, o aviso prévio indenizado. Como incide sobre o salário mensal, que já remunera o repouso, não há DSR sobre ele.",
            "As despesas da mudança correm por conta do empregador (CLT, art. 470). São reembolso, sem natureza salarial: não entram no FGTS nem nos reflexos.",
            "Demonstrativo bruto: INSS, IRRF, atualização monetária e juros não estão incluídos. O FGTS e a multa são depositados pelo empregador e não somam ao total."
        };
        if (MemoriaReflexos.ObservacaoFerias(r) is { } ferias)
            observacoes.Add(ferias);

        var periodo = t.Meses.Count == 1 ? Formato.Competencia(t.Meses[0].Mes.Competencia) : $"{Formato.Competencia(t.Meses[0].Mes.Competencia)} a {Formato.Competencia(t.Meses[^1].Mes.Competencia)}";
        return new DemonstrativoDto("Adicional de transferência", $"Competências {periodo} • adicional de {percentual}",
            destaques, proventos, [], MemoriaReflexos.Informativos(r), memoria, observacoes,
            RotuloProventos: "Adicional e reflexos", RotuloResultado: "Total ao empregado");
    }
}
