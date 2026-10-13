using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>IR no resgate de PGBL ou VGBL pelo regime escolhido, com o outro regime lado a lado.</summary>
public sealed class SimularResgatePrevidenciaUseCase : ISimularDemonstrativoUseCase<SimularResgatePrevidenciaRequest>
{
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularResgatePrevidenciaRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultado = CalculadoraResgatePrevidencia.Calcular(request.Entrada);
        return Task.FromResult(resultado.Falhou ? Result<DemonstrativoDto>.Falha(resultado.Erro) : Montar(resultado.Valor));
    }

    private static Result<DemonstrativoDto> Montar(ApuracaoResgatePrevidencia a)
    {
        var e = a.Entrada;
        var vgbl = e.Tipo == TipoPlanoPrevidencia.Vgbl;
        var regressivo = e.Regime == RegimeTributacaoPrevidencia.Regressivo;
        var plano = vgbl ? "VGBL" : "PGBL";
        var outro = regressivo ? a.ImpostoProgressivo : a.ImpostoRegressivo;
        var aportes = a.Parcelas.Count == 1 ? "1 aporte" : $"{a.Parcelas.Count} aportes";

        var destaques = new List<DestaqueDto>
        {
            new("Valor líquido", Formato.Moeda(a.Liquido), $"Resgate de {Formato.Moeda(a.Resgatado)} menos o IR"),
            new("IR retido", Formato.Moeda(a.Imposto), regressivo ? $"Regressivo, definitivo: {AliquotaMedia(a.Imposto, a.Base)} da base" : "Progressivo: 15% de antecipação"),
            new("Base do IR", Formato.Moeda(a.Base), vgbl ? "Só os rendimentos do VGBL" : "Todo o valor resgatado do PGBL"),
            new(regressivo ? "IR no progressivo" : "IR no regressivo", Formato.Moeda(outro), regressivo ? "15% retidos, com ajuste na declaração" : "Definitivo, pela tabela regressiva")
        };

        VerbaDto[] proventos = [new($"Resgate {(e.ValorResgate is null ? "total" : "parcial")} do {plano}", aportes, a.Resgatado)];
        VerbaDto[] descontos = [regressivo
            ? new("IR regressivo (definitivo)", $"{AliquotaMedia(a.Imposto, a.Base)} da base", a.Imposto)
            : new("IRRF de 15% (antecipação)", $"15% de {Formato.Moeda(a.Base)}", a.Imposto)];
        var informativos = new List<VerbaDto>();
        if (a.SaldoRestante > 0m) informativos.Add(new("Saldo que fica no plano", "Aportes mais recentes", a.SaldoRestante));
        informativos.Add(new(regressivo ? "IR no regime progressivo" : "IR no regime regressivo", "Para comparação", outro));

        var porAporte = a.Parcelas.Select(p =>
        {
            var data = Formato.Data(p.Aporte.Data);
            var baseIr = vgbl
                ? $"base {Formato.Moeda(p.Resgatado)} x rendimentos {Formato.Moeda(Math.Max(0m, p.Aporte.Saldo - p.Aporte.Aportado))} ÷ saldo {Formato.Moeda(p.Aporte.Saldo)} = {Formato.Moeda(p.Base)}"
                : $"base {Formato.Moeda(p.Base)}";
            return new FormulaDto($"Aporte de {data}", $"aportado {Formato.Moeda(p.Aporte.Aportado)}, saldo {Formato.Moeda(p.Aporte.Saldo)}; resgatado {Formato.Moeda(p.Resgatado)}; {baseIr}; {Faixa(p.Aliquota)}: {Formato.PercentualCurto(p.Aliquota)} = {Formato.Moeda(p.Imposto)}");
        }).ToArray();
        var porFaixa = a.Parcelas.GroupBy(p => p.Aliquota).OrderByDescending(grupo => grupo.Key).Select(grupo => new FormulaDto(
            $"{Formato.PercentualCurto(grupo.Key)} ({Faixa(grupo.Key)})",
            $"base {Formato.Moeda(grupo.Sum(p => p.Base))}: IR {Formato.Moeda(grupo.Sum(p => p.Imposto))}")).ToList();
        porFaixa.Add(new("Regime progressivo", $"{Formato.Moeda(a.Base)} x 15% = {Formato.Moeda(a.ImpostoProgressivo)}, retidos como antecipação"));
        var memoria = new List<GrupoMemoriaDto>
        {
            new("Aportes resgatados, dos mais antigos aos mais recentes", $"{aportes}: {Formato.Moeda(a.Resgatado)}", porAporte),
            new("IR por regime", $"Regressivo {Formato.Moeda(a.ImpostoRegressivo)} • progressivo {Formato.Moeda(a.ImpostoProgressivo)}", porFaixa)
        };

        var observacoes = new List<string>
        {
            "Regressivo (Lei 11.053/2004, art. 1º): IR definitivo pelo prazo de cada aporte, de 35% até 2 anos a 10% acima de 10 anos. O resgate consome primeiro os aportes mais antigos, que têm a menor alíquota.",
            "Progressivo (Lei 11.053/2004, art. 3º): 15% retidos no resgate como antecipação. O valor entra como rendimento tributável na declaração anual, com o imposto da tabela progressiva (até 27,5%), e os 15% são compensados: o IR final depende da renda do ano. Use IRPF anual para estimá-lo.",
            "Desde a Lei 14.803/2024, o participante pode escolher o regime até o primeiro resgate ou o primeiro benefício; depois, a escolha é irretratável. Confirme o regime do plano com a seguradora ou a entidade.",
            vgbl
                ? "VGBL: as contribuições não são dedutíveis, e o IR incide só sobre os rendimentos. A base de cada aporte é a parte do resgate proporcional aos rendimentos no saldo dele."
                : "PGBL: as contribuições são dedutíveis na declaração completa até 12% da renda tributável; por isso o IR incide sobre todo o valor resgatado.",
            "O saldo de cada aporte vem do extrato; quando estimado pela rentabilidade, o IR também é estimado. Aposentados com moléstia grave podem ter isenção (Lei 7.713/1988, art. 6º, XIV), que não é aplicada aqui. Taxas de carregamento e de saída e a portabilidade entre planos, que não é resgate, não estão incluídas."
        };

        return new DemonstrativoDto("Resgate de previdência privada", $"{plano} • resgate em {Formato.Data(e.DataResgate)} • regime {(regressivo ? "regressivo" : "progressivo")}",
            destaques, proventos, descontos, informativos, memoria, observacoes,
            RotuloProventos: "Resgate", RotuloResultado: "Valor líquido do resgate",
            Comparativo: new TabelaComparativaDto("Regressivo x progressivo", ["Regressivo", "Progressivo"],
            [
                new("IR retido no resgate", [Formato.Moeda(a.ImpostoRegressivo), Formato.Moeda(a.ImpostoProgressivo)]),
                new("Líquido no resgate", [Formato.Moeda(a.Resgatado - a.ImpostoRegressivo), Formato.Moeda(a.Resgatado - a.ImpostoProgressivo)], true),
                new("Alíquota sobre a base", [AliquotaMedia(a.ImpostoRegressivo, a.Base), "15%"]),
                new("Natureza do IR", ["Definitivo", "Antecipação: ajuste na declaração"])
            ]));
    }

    private static string AliquotaMedia(decimal imposto, decimal baseIr) =>
        baseIr == 0m ? "0%" : Formato.Percentual(decimal.Round(imposto / baseIr * 100m, 2, MidpointRounding.AwayFromZero));

    private static string Faixa(decimal aliquota) => aliquota switch
    {
        35m => "até 2 anos",
        30m => "de 2 a 4 anos",
        25m => "de 4 a 6 anos",
        20m => "de 6 a 8 anos",
        15m => "de 8 a 10 anos",
        _ => "acima de 10 anos"
    };
}
