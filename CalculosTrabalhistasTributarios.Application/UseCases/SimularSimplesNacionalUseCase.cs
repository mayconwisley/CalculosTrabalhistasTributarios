using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// DAS do Simples Nacional de um período, com a receita e a folha de 12 meses, o fator r e o anexo aplicável. Nas
/// atividades sujeitas ao fator r, compara os Anexos III e V e estima o efeito de elevar o pró-labore até os 28%.
/// </summary>
public sealed class SimularSimplesNacionalUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularSimplesNacionalRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularSimplesNacionalRequest request, CancellationToken cancellationToken)
    {
        if (request.ProLaboreAtual < 0m || request.ProLaboreAtual > 1_000_000_000m || decimal.Round(request.ProLaboreAtual, 2) != request.ProLaboreAtual
            || request.Dependentes is < 0 or > 99)
            return Erro.Validacao("Informe um pró-labore positivo ou zero, com dois decimais, e de 0 a 99 dependentes.");
        var resultado = CalculadoraSimplesNacional.Calcular(request.Entrada);
        if (resultado.Falhou)
            return resultado.Erro;
        var s = resultado.Valor;
        var e = request.Entrada;

        AnaliseProLabore? analise = null;
        if (s.FatorR is not null && s.AnexoIII is { } anexoIII && s.AnexoV is { } anexoV && s.Janela.Regra != RegraReceitaSimples.PrimeiraFaixa && s.Rbt12 > 0m)
        {
            var consulta = await tributacaoConsulta.ObterTabelasAsync(e.PeriodoApuracao, cancellationToken);
            if (consulta.Falhou)
                return consulta.Erro;
            analise = Analisar(s, consulta.Valor, request, anexoIII, anexoV);
        }

        var anexo = s.Aliquota.Anexo;
        var destaques = new List<DestaqueDto>
        {
            new("DAS do mês", Formato.Moeda(s.Das), $"Anexo {anexo}, faixa {s.Aliquota.Faixa}"),
            new("Alíquota efetiva", Formato.Percentual(Math.Round(s.Aliquota.AliquotaEfetiva, 4)), $"Nominal de {Formato.Percentual(s.Aliquota.AliquotaNominal)}"),
            new("Receita de 12 meses (RBT12)", s.Janela.Regra == RegraReceitaSimples.PrimeiraFaixa ? "1ª faixa" : Formato.Moeda(s.Rbt12), DescreverRegra(s.Janela))
        };
        if (s.FatorR is { } fator)
            destaques.Add(new("Fator r", Formato.Percentual(Math.Round(fator * 100m, 2)), fator >= 0.28m ? "28% ou mais: Anexo III" : "Abaixo de 28%: Anexo V"));
        else if (s.CppForaDoDas > 0m)
            destaques.Add(new("CPP fora do DAS", Formato.Moeda(s.CppForaDoDas), "Anexo IV, estimativa de 20%"));

        var descontos = new List<VerbaDto> { new($"DAS — Simples Nacional (Anexo {anexo})", Formato.Percentual(Math.Round(s.Aliquota.AliquotaEfetiva, 4)), s.Das) };
        if (s.CppForaDoDas > 0m)
            descontos.Add(new("CPP do Anexo IV (estimativa)", "20% das remunerações", s.CppForaDoDas));
        var informativos = new List<VerbaDto>();
        if (s.Janela.Regra != RegraReceitaSimples.PrimeiraFaixa)
        {
            informativos.Add(new("Receita bruta de 12 meses (RBT12)", DescreverRegra(s.Janela), s.Rbt12));
            if (s.FatorR is not null)
                informativos.Add(new("Folha com encargos de 12 meses (FS12)", "Base do fator r", s.Fs12));
        }
        if (analise is { FolhaAdicionalMensal: > 0m } faltante)
            informativos.Add(new("Pró-labore adicional para o fator r de 28%", "Por mês, mantida a receita", faltante.FolhaAdicionalMensal));

        var memoria = new List<GrupoMemoriaDto> { Janela(s) };
        if (s.FatorR is { } r)
            memoria.Add(new GrupoMemoriaDto("Fator r", $"{Formato.Percentual(Math.Round(r * 100m, 2))} • Anexo {anexo}",
            [
                new("Cálculo", s.CasoFatorR ?? $"{Formato.Moeda(s.Fs12)} (FS12) ÷ {Formato.Moeda(s.Rbt12)} (RBT12) = {Formato.Percentual(Math.Round(r * 100m, 4))}"),
                new("Anexo", r >= 0.28m ? "Igual ou maior que 28%: Anexo III (LC 123/2006, art. 18, § 5º-J)" : "Menor que 28%: Anexo V (LC 123/2006, art. 18, § 5º-M)")
            ]));
        memoria.Add(new GrupoMemoriaDto("Alíquota e DAS", $"DAS: {Formato.Moeda(s.Das)}",
        [
            new("Alíquota efetiva", Efetiva(s, s.Aliquota)),
            new("DAS do mês", $"{Formato.Moeda(s.ReceitaMes)} × {Formato.Percentual(Math.Round(s.Aliquota.AliquotaEfetiva, 4))} = {Formato.Moeda(s.Das)}"),
            .. s.CppForaDoDas > 0m ? [new FormulaDto("CPP fora do DAS (Anexo IV)", $"{Formato.Moeda(e.RemuneracoesMes)} × 20% = {Formato.Moeda(s.CppForaDoDas)}, sem o RAT, recolhida em guia própria")] : Array.Empty<FormulaDto>()
        ]));
        if (analise is not null)
            memoria.Add(analise.Memoria);

        var observacoes = new List<string>
        {
            s.Janela.Regra2027
                ? "Desde 01/2027, a receita (RBT12) e a folha (FS12) são as dos 12 meses antecedentes ao mês anterior ao do período de apuração: o mês imediatamente anterior fica de fora (Resolução CGSN 140/2018, arts. 21 e 26, na redação da Resolução CGSN 190/2026)."
                : "A receita (RBT12) e a folha (FS12) são as dos 12 meses anteriores ao período de apuração (Resolução CGSN 140/2018, arts. 21 e 26). A partir de 01/2027, a Resolução CGSN 190/2026 desloca a janela um mês para trás.",
            "A folha com encargos soma salários e pró-labore pagos, informados no eSocial, com o 13º na competência da contribuição, mais a CPP e o FGTS efetivamente recolhidos. Aluguéis e lucros distribuídos não entram (art. 26, §§ 1º a 3º).",
            "O DAS é estimado pela alíquota efetiva sobre a receita do mês, para uma única atividade. Receitas em mais de um anexo, exportação, ISS retido, substituição tributária e o valor exato do PGDAS-D, que reparte o imposto por tributo, não são simulados."
        };
        if (s.AcimaDoSublimite)
            observacoes.Insert(0, $"A RBT12 passa do sublimite de {Formato.Moeda(SimplesNacional.SublimiteIcmsIss)}: o ICMS e o ISS deixam de ser recolhidos no DAS e seguem as regras do estado e do município. O valor mostrado usa a alíquota total como referência.");
        if (anexo == AnexoSimples.IV)
            observacoes.Add("No Anexo IV, a contribuição patronal (CPP) não está no DAS: são 20% sobre salários e pró-labore, mais o RAT sobre os salários, em guia própria (LC 123/2006, art. 18, § 5º-C). A estimativa não inclui o RAT.");
        if (s.Janela.Regra is RegraReceitaSimples.PrimeiroMes or RegraReceitaSimples.MediaInicio or RegraReceitaSimples.PrimeiraFaixa)
            observacoes.Add("Em início de atividade, a receita e a folha de 12 meses são proporcionalizadas pelos meses de atividade (Resolução CGSN 140/2018, arts. 22 e 26, § 4º).");
        if (analise is not null)
            observacoes.Add(analise.Observacao);

        return new DemonstrativoDto("Simples Nacional e fator r", $"Período de apuração {Formato.Competencia(e.PeriodoApuracao)} • Anexo {anexo}",
            destaques, [new("Receita bruta do mês", "Período de apuração", s.ReceitaMes)], descontos, informativos, memoria, observacoes,
            RotuloProventos: "Receita", RotuloResultado: "Receita após os tributos do Simples",
            Comparativo: s.AnexoIII is { } iii && s.AnexoV is { } v ? Comparar(s, iii, v) : null);
    }

    private sealed record AnaliseProLabore(decimal FolhaAdicionalMensal, GrupoMemoriaDto Memoria, string Observacao);

    /// <summary>
    /// No Anexo V, o pró-labore mensal que falta para a folha de 12 meses chegar a 28% da receita, mantida a receita, e
    /// o saldo entre a economia de DAS e o INSS e o IRRF do sócio sobre o acréscimo. No Anexo III, a folga até os 28%.
    /// </summary>
    private static AnaliseProLabore Analisar(ApuracaoSimples s, TabelasDaCompetencia tabelas, SimularSimplesNacionalRequest r, AliquotaSimples iii, AliquotaSimples v)
    {
        var meta = s.Rbt12 * SimplesNacional.FatorRMinimo / 100m;
        var dasIII = s.DasPelo(iii);
        var dasV = s.DasPelo(v);
        if (s.Aliquota.Anexo == AnexoSimples.III)
        {
            var folga = Math.Max(0m, s.Fs12 - meta);
            return new AnaliseProLabore(0m, new GrupoMemoriaDto("Folha e fator r", $"Folga de {Formato.Moeda(folga)} em 12 meses",
            [
                new("Folha mínima de 12 meses", $"{Formato.Moeda(s.Rbt12)} × 28% = {Formato.Moeda(CalculadoraTributacao.Arredondar(meta))}"),
                new("Folga", $"{Formato.Moeda(s.Fs12)} - {Formato.Moeda(CalculadoraTributacao.Arredondar(meta))} = {Formato.Moeda(CalculadoraTributacao.Arredondar(folga))}: abaixo disso, a atividade passa ao Anexo V"),
                new("Economia de estar no Anexo III", $"{Formato.Moeda(dasV)} (Anexo V) - {Formato.Moeda(dasIII)} (Anexo III) = {Formato.Moeda(dasV - dasIII)} no DAS deste mês")
            ]), "A folha está acima de 28% da receita. Se a receita crescer ou a folha diminuir, confira o fator r antes de cada DAS: abaixo de 28%, a atividade volta ao Anexo V.");
        }

        var adicional = Math.Ceiling((meta - s.Fs12) / 12m * 100m) / 100m;
        var proLabore = r.ProLaboreAtual;
        var inssAtual = tabelas.CalcularInssContribuinteIndividual(proLabore);
        var inssNovo = tabelas.CalcularInssContribuinteIndividual(proLabore + adicional);
        var irrfAtual = tabelas.CalcularIrrf(proLabore, inssAtual.Valor, r.Dependentes).Imposto;
        var irrfNovo = tabelas.CalcularIrrf(proLabore + adicional, inssNovo.Valor, r.Dependentes).Imposto;
        var custo = inssNovo.Valor - inssAtual.Valor + irrfNovo - irrfAtual;
        var economia = dasV - dasIII;
        var saldo = economia - custo;
        var defasagem = s.Janela.Regra2027 ? "no segundo mês seguinte" : "no mês seguinte";
        return new AnaliseProLabore(adicional, new GrupoMemoriaDto("Pró-labore para o fator r de 28%", $"Saldo de {Formato.Moeda(saldo)} por mês",
        [
            new("Folha que falta em 12 meses", $"{Formato.Moeda(s.Rbt12)} × 28% - {Formato.Moeda(s.Fs12)} = {Formato.Moeda(CalculadoraTributacao.Arredondar(meta - s.Fs12))}"),
            new("Pró-labore adicional", $"{Formato.Moeda(CalculadoraTributacao.Arredondar(meta - s.Fs12))} ÷ 12 = {Formato.Moeda(adicional)} por mês, arredondado para cima; a folha de 12 meses só alcança os 28% depois de 12 meses com o acréscimo"),
            new("INSS do sócio (11%)", $"{Formato.Moeda(inssAtual.Valor)} sobre {Formato.Moeda(proLabore)} → {Formato.Moeda(inssNovo.Valor)} sobre {Formato.Moeda(proLabore + adicional)}: + {Formato.Moeda(inssNovo.Valor - inssAtual.Valor)}"),
            new("IRRF do pró-labore", $"{Formato.Moeda(irrfAtual)} → {Formato.Moeda(irrfNovo)}: + {Formato.Moeda(irrfNovo - irrfAtual)}"),
            new("Economia no DAS", $"{Formato.Moeda(dasV)} (Anexo V) - {Formato.Moeda(dasIII)} (Anexo III) = {Formato.Moeda(economia)} por mês, com esta receita"),
            new("Saldo estimado", $"{Formato.Moeda(economia)} - {Formato.Moeda(custo)} (INSS e IRRF a mais) = {Formato.Moeda(saldo)} por mês, depois que o fator r alcançar 28%")
        ]),
        $"Para sair do Anexo V, a folha de 12 meses precisa crescer {Formato.Moeda(CalculadoraTributacao.Arredondar(meta - s.Fs12))}. Com {Formato.Moeda(adicional)} a mais de pró-labore por mês, mantida a receita, o fator r chega a 28% depois de 12 meses; para chegar antes, o acréscimo dos primeiros meses precisa ser maior. Cada pagamento entra no fator r {defasagem}. O pró-labore não paga CPP nos Anexos III e V, porque ela está no DAS, mas sofre INSS de 11% e IRRF do sócio. O saldo é uma estimativa por mês e não considera a tributação de lucros distribuídos.");
    }

    private static TabelaComparativaDto Comparar(ApuracaoSimples s, AliquotaSimples iii, AliquotaSimples v)
    {
        string Marca(AliquotaSimples item) => s.Aliquota.Anexo == item.Anexo ? " (aplicado)" : "";
        return new TabelaComparativaDto("Anexo III e Anexo V", [$"Anexo III{Marca(iii)}", $"Anexo V{Marca(v)}"],
        [
            new("Faixa", [$"{iii.Faixa}ª", $"{v.Faixa}ª"]),
            new("Alíquota nominal", [Formato.Percentual(iii.AliquotaNominal), Formato.Percentual(v.AliquotaNominal)]),
            new("Parcela a deduzir", [Formato.Moeda(iii.ParcelaDeduzir), Formato.Moeda(v.ParcelaDeduzir)]),
            new("Alíquota efetiva", [Formato.Percentual(Math.Round(iii.AliquotaEfetiva, 4)), Formato.Percentual(Math.Round(v.AliquotaEfetiva, 4))]),
            new("DAS do mês", [Formato.Moeda(s.DasPelo(iii)), Formato.Moeda(s.DasPelo(v))], true)
        ]);
    }

    private static GrupoMemoriaDto Janela(ApuracaoSimples s)
    {
        var formulas = new List<FormulaDto> { new("Regra", DescreverRegra(s.Janela)) };
        formulas.AddRange(s.MesesConsiderados.Select(mes => new FormulaDto(Formato.Competencia(mes.Competencia),
            $"Receita {Formato.Moeda(mes.Receita)} • folha com encargos {Formato.Moeda(mes.Folha)}")));
        var receitas = s.MesesConsiderados.Sum(mes => mes.Receita);
        var folhas = s.MesesConsiderados.Sum(mes => mes.Folha);
        var n = s.MesesConsiderados.Count;
        switch (s.Janela.Regra)
        {
            case RegraReceitaSimples.PrimeiroMes:
                formulas.Add(new("RBT12", $"{Formato.Moeda(s.ReceitaMes)} (receita do mês) × 12 = {Formato.Moeda(s.Rbt12)}"));
                formulas.Add(new("FS12", $"Folha do mês × 12 = {Formato.Moeda(s.Fs12)}"));
                break;
            case RegraReceitaSimples.MediaInicio:
                formulas.Add(new("RBT12", $"{Formato.Moeda(receitas)} ÷ {n} × 12 = {Formato.Moeda(s.Rbt12)}"));
                formulas.Add(new("FS12", $"{Formato.Moeda(folhas)} ÷ {n} × 12 = {Formato.Moeda(s.Fs12)}"));
                break;
            case RegraReceitaSimples.PrimeiraFaixa:
                formulas.Add(new("Alíquota", "1º ou 2º mês de atividade: alíquota nominal da 1ª faixa, sem RBT12 (art. 22, § 2º, I)"));
                break;
            default:
                formulas.Add(new("RBT12", $"Soma das receitas = {Formato.Moeda(s.Rbt12)}"));
                formulas.Add(new("FS12", $"Soma das folhas com encargos = {Formato.Moeda(s.Fs12)}"));
                break;
        }
        var periodo = n == 0 ? "sem meses anteriores" : n == 1 ? Formato.Competencia(s.MesesConsiderados[0].Competencia) : $"{Formato.Competencia(s.MesesConsiderados[0].Competencia)} a {Formato.Competencia(s.MesesConsiderados[^1].Competencia)}";
        return new GrupoMemoriaDto("Receita e folha de 12 meses", periodo, formulas);
    }

    private static string Efetiva(ApuracaoSimples s, AliquotaSimples a) =>
        s.Janela.Regra == RegraReceitaSimples.PrimeiraFaixa || s.Rbt12 <= 0m
            ? $"1ª faixa do Anexo {a.Anexo}: alíquota nominal de {Formato.Percentual(a.AliquotaNominal)}"
            : a.Faixa == 1
            ? $"1ª faixa do Anexo {a.Anexo}: {Formato.Percentual(a.AliquotaNominal)}, sem parcela a deduzir"
            : $"{a.Faixa}ª faixa do Anexo {a.Anexo}: ({Formato.Moeda(s.Rbt12)} × {Formato.Percentual(a.AliquotaNominal)} - {Formato.Moeda(a.ParcelaDeduzir)}) ÷ {Formato.Moeda(s.Rbt12)} = {Formato.Percentual(Math.Round(a.AliquotaEfetiva, 4))}";

    private static string DescreverRegra(JanelaSimples janela) => janela.Regra switch
    {
        RegraReceitaSimples.PrimeiroMes => "1º mês de atividade: receita do mês × 12",
        RegraReceitaSimples.MediaInicio => $"{janela.MesDeAtividade}º mês de atividade: média × 12",
        RegraReceitaSimples.PrimeiraFaixa => $"{janela.MesDeAtividade}º mês de atividade: 1ª faixa",
        _ => janela.Regra2027 ? "12 meses antes do mês anterior" : "12 meses anteriores"
    };
}
