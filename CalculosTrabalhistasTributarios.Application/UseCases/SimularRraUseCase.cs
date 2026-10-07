using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// IRRF sobre rendimentos recebidos acumuladamente: anos anteriores pela tabela acumulada, em separado e de forma
/// exclusiva, e o ano do pagamento no mês do recebimento, com memória própria para os rendimentos normais do mês.
/// </summary>
public sealed class SimularRraUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularRraRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularRraRequest request, CancellationToken cancellationToken)
    {
        var p = request.Pagamento;
        var mesPagamento = new DateOnly(p.DataPagamento.Year, p.DataPagamento.Month, 1);
        var consulta = await tributacaoConsulta.ObterTabelasAsync(mesPagamento, cancellationToken);
        if (consulta.Falhou)
            return consulta.Erro;
        var tabelas = consulta.Valor;
        var resultado = CalculadoraRra.Calcular(p, tabelas);
        if (resultado.Falhou)
            return resultado.Erro;
        var rra = resultado.Valor;
        var anteriores = rra.AnosAnteriores;
        var doAno = rra.AnoPagamento;
        var ano = p.DataPagamento.Year;

        var destaques = new List<DestaqueDto>();
        if (anteriores is not null)
            destaques.Add(new("IR de anos anteriores", Formato.Moeda(anteriores.Irrf.Imposto), $"Exclusivo na fonte • NM {Formato.Numero(anteriores.Irrf.Meses)}"));
        if (doAno is not null)
            destaques.Add(new($"IR da parte de {ano}", Formato.Moeda(doAno.Imposto),
                p.Origem == OrigemPagamentoRra.JusticaFederal ? "3% do precatório ou RPV, antecipação" : "No mês do pagamento, antecipação"));
        destaques.Add(new("IR estimado do pagamento", Formato.Moeda(rra.Imposto), "Soma das partes"));
        destaques.Add(new("Total recebido", Formato.Moeda(rra.TotalRecebido), rra.JurosDeMora > 0m ? "Com juros de mora não tributáveis" : "Parcelas e correção"));

        var proventos = new List<VerbaDto>();
        if (anteriores is not null)
            proventos.Add(new("Rendimentos de anos anteriores", $"{Formato.Lista(anteriores.Anos.Select(item => item.ToString()).ToArray())} • NM {Formato.Numero(anteriores.Irrf.Meses)}", anteriores.Rendimentos));
        if (doAno is not null)
            proventos.Add(new($"Rendimentos de {ano}", "Ano do pagamento", doAno.Rendimentos));
        if (rra.JurosDeMora > 0m)
            proventos.Add(new("Juros de mora", "Não tributáveis", rra.JurosDeMora));

        var descontos = new List<VerbaDto>();
        if (anteriores is not null && anteriores.Irrf.Imposto > 0m)
            descontos.Add(new("IRRF do RRA de anos anteriores", "Tabela acumulada", anteriores.Irrf.Imposto));
        if (doAno is not null && doAno.Imposto > 0m)
            descontos.Add(new($"IRRF da parte de {ano}", p.Origem == OrigemPagamentoRra.JusticaFederal ? "3%" : "Tabela mensal", doAno.Imposto));
        var inss = (anteriores?.Inss ?? 0m) + (doAno?.Inss ?? 0m);
        var pensao = (anteriores?.Pensao ?? 0m) + (doAno?.Pensao ?? 0m);
        if (inss > 0m) descontos.Add(new("Contribuição previdenciária", "Informada", inss));
        if (pensao > 0m) descontos.Add(new("Pensão alimentícia", "Informada", pensao));

        var informativos = new List<VerbaDto>();
        if (p.DespesasJudiciais > 0m)
            informativos.Add(new("Despesas com a ação judicial", "Deduzidas da base", p.DespesasJudiciais));
        if (anteriores is not null)
            informativos.Add(new("Base de cálculo de anos anteriores", $"NM {Formato.Numero(anteriores.Irrf.Meses)}", anteriores.Irrf.BaseCalculo));

        var memoria = new List<GrupoMemoriaDto> { Parcelas(p, anteriores, doAno) };
        if (p.CorrecaoMonetaria > 0m || p.DespesasJudiciais > 0m)
            memoria.Add(Rateio(p, rra));
        if (anteriores is not null)
            memoria.Add(AnosAnteriores(anteriores, tabelas));
        if (doAno is not null)
            memoria.AddRange(AnoPagamento(p, doAno, ano));

        var observacoes = new List<string>
        {
            $"Os valores de anos anteriores a {ano} são tributados exclusivamente na fonte, no mês do pagamento e em separado dos demais rendimentos, pela tabela mensal de {Formato.Competencia(mesPagamento)} multiplicada pela quantidade de meses (Lei 7.713/1988, art. 12-A; IN RFB 1.500/2014, arts. 36 e 37). Dependentes e desconto simplificado não se aplicam; deduzem-se só as despesas com a ação, a contribuição previdenciária oficial e a pensão alimentícia judicial (arts. 38 e 39).",
            "Na declaração anual, o contribuinte pode optar por levar os RRA de anos anteriores ao ajuste. A opção é irretratável e abrange todos os RRA do ano; o imposto retido passa a ser antecipação (IN RFB 1.500/2014, art. 41).",
            "Cada competência mensal conta um mês e o 13º salário de cada ano conta mais um (art. 37, § 1º). Se este pagamento é uma das parcelas de um mesmo RRA pago em meses diferentes, informe o total de todas as parcelas de anos anteriores para dividir os meses pelo valor (art. 45, I).",
            "Simulação de uma fonte pagadora. Confira as parcelas, a natureza dos valores e o comprovante de rendimentos do pagador antes de declarar ou recolher."
        };
        if (doAno is not null)
            observacoes.Insert(1, p.Origem == OrigemPagamentoRra.JusticaFederal
                ? $"A parte de {ano} paga em precatório ou RPV da Justiça Federal sofre retenção de 3% sobre o montante, sem deduções, como antecipação do ajuste anual (IN RFB 1.500/2014, art. 25). O valor entra na declaração como rendimento do ano."
                : $"A parte de {ano} é tributada no mês do recebimento com os rendimentos normais pagos pela mesma fonte, diminuída das despesas com a ação, e entra no ajuste anual (Lei 7.713/1988, art. 12-B). O IR atribuído a ela é o acréscimo que causa no IRRF do mês.");
        if (rra.JurosDeMora > 0m)
            observacoes.Add("Os juros de mora pelo atraso no pagamento de remuneração do trabalho não são tributáveis (IN RFB 1.500/2014, art. 36, § 4º, e STF, Tema 808). Confira se os juros do seu caso têm essa natureza; a correção monetária é tributável e acompanha as parcelas.");
        if (anteriores is not null && anteriores.Irrf.ReducaoAplicavel)
            observacoes.Add("A redução da Lei 15.270/2025 foi aplicada à parte de anos anteriores com os limites e os valores multiplicados pela quantidade de meses. O art. 37 da IN RFB 1.500/2014 manda observar a tabela de redução, mas não detalha essa multiplicação; confira o critério da fonte pagadora no comprovante.");
        else if (anteriores is not null && tabelas.TemReducaoMensal)
            observacoes.Add("A redução da Lei 15.270/2025 não foi aplicada à parte de anos anteriores, conforme a opção escolhida. O art. 37 da IN RFB 1.500/2014 manda observar a tabela de redução, mas não detalha como combiná-la com a quantidade de meses.");

        return new DemonstrativoDto("Rendimentos recebidos acumuladamente (RRA)",
            $"Pagamento em {Formato.Data(p.DataPagamento)} • Tabelas de {Formato.Competencia(mesPagamento)}",
            destaques, proventos, descontos, informativos, memoria, observacoes,
            RotuloProventos: "Valores recebidos", RotuloResultado: "Valor após IRRF, INSS e pensão");
    }

    private static GrupoMemoriaDto Parcelas(PagamentoRra p, ParteAnosAnterioresRra? anteriores, ParteAnoPagamentoRra? doAno)
    {
        var formulas = p.Parcelas.GroupBy(item => item.Competencia.Year).OrderBy(grupo => grupo.Key).Select(grupo =>
        {
            var mensais = grupo.Where(item => item.Tipo == TipoParcelaRra.Mensal).Select(item => item.Competencia).Distinct().Count();
            var decimo = grupo.Any(item => item.Tipo == TipoParcelaRra.DecimoTerceiro);
            var parte = grupo.Key < p.DataPagamento.Year ? "anos anteriores" : "ano do pagamento";
            return new FormulaDto($"{grupo.Key} ({parte})",
                $"{mensais} competência(s) mensal(is){(decimo ? " e 13º salário" : "")}: {Formato.Moeda(grupo.Sum(item => item.Valor))}");
        }).ToList();
        if (anteriores is not null)
        {
            var decimos = anteriores.MesesTotais - p.Parcelas.Where(item => item.Competencia.Year < p.DataPagamento.Year && item.Tipo == TipoParcelaRra.Mensal).Select(item => item.Competencia).Distinct().Count();
            formulas.Add(new("Quantidade de meses (NM)", $"{anteriores.MesesTotais - decimos} competência(s) mensal(is) + {decimos} 13º salário(s) = {anteriores.MesesTotais}"));
            if (anteriores.Irrf.Meses != anteriores.MesesTotais && p.TotalParcelasAnosAnteriores is { } total)
                formulas.Add(new("NM desta parcela (art. 45, I)", $"{anteriores.MesesTotais} × {Formato.Moeda(anteriores.Parcelas)} ÷ {Formato.Moeda(total)} = {Formato.Numero(anteriores.Irrf.Meses)}, com uma casa decimal"));
        }
        var destaque = $"{(anteriores is null ? "Sem anos anteriores" : $"NM {Formato.Numero(anteriores.Irrf.Meses)} de anos anteriores")}{(doAno is null ? "" : $" • {Formato.Moeda(doAno.Parcelas)} do ano do pagamento")}";
        return new GrupoMemoriaDto("Parcelas e meses de referência", destaque, formulas);
    }

    private static GrupoMemoriaDto Rateio(PagamentoRra p, ApuracaoRra rra)
    {
        var formulas = new List<FormulaDto>();
        var somaParcelas = p.Parcelas.Sum(item => item.Valor);
        if (p.CorrecaoMonetaria > 0m)
            formulas.Add(new("Correção monetária", $"{Formato.Moeda(p.CorrecaoMonetaria)} rateada pelas parcelas ({Formato.Moeda(somaParcelas)}): {Formato.Moeda(rra.AnosAnteriores?.Correcao ?? 0m)} de anos anteriores e {Formato.Moeda(rra.AnoPagamento?.Correcao ?? 0m)} do ano do pagamento"));
        if (p.DespesasJudiciais > 0m)
        {
            formulas.Add(new("Despesas com a ação", $"{Formato.Moeda(p.DespesasJudiciais)} rateadas pelo total recebido ({Formato.Moeda(rra.TotalRecebido)}): {Formato.Moeda(rra.AnosAnteriores?.Despesas ?? 0m)} de anos anteriores e {Formato.Moeda(rra.AnoPagamento?.Despesas ?? 0m)} do ano do pagamento"));
            if (rra.DespesasNaoDedutiveis > 0m)
                formulas.Add(new("Parte dos juros não tributáveis", $"{Formato.Moeda(rra.DespesasNaoDedutiveis)} das despesas correspondem aos juros de mora e não são deduzidos"));
        }
        return new GrupoMemoriaDto("Rateio da correção e das despesas", "Proporcional aos valores", formulas);
    }

    private static GrupoMemoriaDto AnosAnteriores(ParteAnosAnterioresRra parte, TabelasDaCompetencia tabelas)
    {
        var irrf = parte.Irrf;
        var meses = Formato.Numero(irrf.Meses);
        var formulas = new List<FormulaDto>
        {
            new("Rendimentos tributáveis", $"{Formato.Moeda(parte.Parcelas)} (parcelas) + {Formato.Moeda(parte.Correcao)} (correção) - {Formato.Moeda(parte.Despesas)} (despesas com a ação) = {Formato.Moeda(irrf.RendimentosTributaveis)}"),
            new("Base de cálculo", $"{Formato.Moeda(irrf.RendimentosTributaveis)} - {Formato.Moeda(parte.Inss)} (contribuição previdenciária) - {Formato.Moeda(parte.Pensao)} (pensão alimentícia) = {Formato.Moeda(irrf.BaseCalculo)}")
        };
        formulas.AddRange(irrf.Faixas.Select(faixa => new FormulaDto($"Faixa {faixa.Faixa} da tabela acumulada",
            $"{Formato.Moeda(faixa.BaseCalculada)} × {Formato.Percentual(faixa.Aliquota)} = {Formato.Moeda(faixa.Imposto)}")));
        formulas.Add(new("Imposto pela tabela acumulada", irrf.Aliquota == 0m
            ? $"{Formato.Moeda(irrf.BaseCalculo)} está na faixa isenta multiplicada por {meses}: {Formato.Moeda(0m)}"
            : $"{Formato.Moeda(irrf.BaseCalculo)} × {Formato.Percentual(irrf.Aliquota)} - ({irrf.ParcelaADeduzirMensal.ToString("#,##0.00###", Formato.Cultura)} × {meses}) = {Formato.Moeda(irrf.ImpostoAntesReducao)}"));
        if (irrf.ReducaoAplicavel)
            formulas.Add(new("Redução da Lei 15.270/2025", $"Limites de rendimento e valores multiplicados por {meses}, sobre {Formato.Moeda(irrf.RendimentosTributaveis)}: {Formato.Moeda(irrf.Reducao)}"));
        else if (tabelas.TemReducaoMensal)
            formulas.Add(new("Redução da Lei 15.270/2025", "Não aplicada, conforme a opção escolhida"));
        formulas.Add(new("IRRF exclusivo na fonte", $"{Formato.Moeda(irrf.ImpostoAntesReducao)} - {Formato.Moeda(irrf.Reducao)} = {Formato.Moeda(irrf.Imposto)}"));
        return new GrupoMemoriaDto("RRA de anos anteriores", $"IRRF: {Formato.Moeda(irrf.Imposto)}", formulas);
    }

    private static IEnumerable<GrupoMemoriaDto> AnoPagamento(PagamentoRra p, ParteAnoPagamentoRra parte, int ano)
    {
        if (p.Origem == OrigemPagamentoRra.JusticaFederal)
        {
            yield return new GrupoMemoriaDto($"Parte de {ano} (Justiça Federal)", $"IRRF: {Formato.Moeda(parte.Imposto)}",
            [
                new("Montante tributável", $"{Formato.Moeda(parte.Parcelas)} (parcelas) + {Formato.Moeda(parte.Correcao)} (correção) = {Formato.Moeda(parte.Rendimentos)}"),
                new("IRRF de 3%", $"{Formato.Moeda(parte.Rendimentos)} × 3% = {Formato.Moeda(parte.Imposto)}, sem deduções")
            ]);
            yield break;
        }

        var rendimentos = parte.Rendimentos - parte.Despesas;
        var comOutros = parte.ComOutros!;
        var rotulo = p.OutrosRendimentosMes > 0m ? $"RRA de {ano} e rendimentos normais" : $"RRA de {ano}";
        var grupo = MemoriaTributaria.Irrf($"Mês do pagamento com a parte de {ano}", comOutros, rotulo);
        yield return grupo with
        {
            Formulas =
            [
                new($"Parte de {ano}", $"{Formato.Moeda(parte.Parcelas)} (parcelas) + {Formato.Moeda(parte.Correcao)} (correção) - {Formato.Moeda(parte.Despesas)} (despesas com a ação) = {Formato.Moeda(rendimentos)}"),
                .. p.OutrosRendimentosMes > 0m ? [new FormulaDto("Com os rendimentos normais", $"{Formato.Moeda(rendimentos)} + {Formato.Moeda(p.OutrosRendimentosMes)} = {Formato.Moeda(comOutros.Rendimentos)}")] : Array.Empty<FormulaDto>(),
                .. grupo.Formulas,
                new($"IRRF atribuído à parte de {ano}", $"{Formato.Moeda(comOutros.Imposto)} - {Formato.Moeda(parte.SoOutros?.Imposto ?? 0m)} (IRRF sem o RRA) = {Formato.Moeda(parte.Imposto)}")
            ]
        };
        if (parte.SoOutros is { } soOutros)
            yield return MemoriaTributaria.Irrf("Rendimentos normais do mês, sem o RRA", soOutros, "rendimentos normais");
    }
}
