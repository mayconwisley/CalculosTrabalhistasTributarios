using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// IRRF sobre rendimentos recebidos acumuladamente (RRA) do trabalho e de aposentadoria ou pensão, separando as duas
/// regras da Receita Federal:
/// <list type="bullet">
/// <item>anos anteriores ao do pagamento: tributação exclusiva na fonte, em separado dos demais rendimentos do mês, pela
/// tabela progressiva multiplicada pela quantidade de meses (Lei 7.713/1988, art. 12-A; IN RFB 1.500/2014, arts. 36 a 39);</item>
/// <item>ano do pagamento: tributação no mês do recebimento, somada aos demais rendimentos da mesma fonte e sujeita ao
/// ajuste anual (Lei 7.713/1988, art. 12-B; IN RFB 1.500/2014, art. 44), ou 3% no precatório e RPV da Justiça Federal (art. 25).</item>
/// </list>
/// Correção monetária e despesas judiciais são rateadas entre as partes pelo valor; a parte das despesas que cabe aos
/// juros de mora não tributáveis não é dedutível.
/// </summary>
public static class CalculadoraRra
{
    public const int LimiteParcelas = 600;
    private const decimal LimiteValor = 1_000_000_000m;
    private const decimal AliquotaJusticaFederal = 3m;

    public static Result<ApuracaoRra> Calcular(PagamentoRra p, TabelasDaCompetencia tabelas)
    {
        var validacao = Validar(p);
        if (validacao.Falhou)
            return validacao.Erro;

        var anoPagamento = p.DataPagamento.Year;
        var anteriores = p.Parcelas.Where(item => item.Competencia.Year < anoPagamento).ToArray();
        var doAno = p.Parcelas.Where(item => item.Competencia.Year == anoPagamento).ToArray();
        var somaAnteriores = anteriores.Sum(item => item.Valor);
        var somaDoAno = doAno.Sum(item => item.Valor);
        var somaParcelas = somaAnteriores + somaDoAno;

        var correcaoAnteriores = CalculadoraTributacao.Arredondar(p.CorrecaoMonetaria * somaAnteriores / somaParcelas);
        var correcaoDoAno = p.CorrecaoMonetaria - correcaoAnteriores;
        var totalRecebido = somaParcelas + p.CorrecaoMonetaria + p.JurosDeMora;
        var despesasAnteriores = CalculadoraTributacao.Arredondar(p.DespesasJudiciais * (somaAnteriores + correcaoAnteriores) / totalRecebido);
        var despesasDoAno = CalculadoraTributacao.Arredondar(p.DespesasJudiciais * (somaDoAno + correcaoDoAno) / totalRecebido);
        var despesasNaoDedutiveis = p.DespesasJudiciais - despesasAnteriores - despesasDoAno;

        ParteAnosAnterioresRra? parteAnteriores = null;
        if (anteriores.Length > 0)
        {
            var mesesTotais = anteriores.Where(item => item.Tipo == TipoParcelaRra.Mensal).Select(item => item.Competencia).Distinct().Count()
                + anteriores.Where(item => item.Tipo == TipoParcelaRra.DecimoTerceiro).Select(item => item.Competencia.Year).Distinct().Count();
            // Parcelas de um mesmo RRA pagas em meses distintos dividem os meses pelo valor, com uma casa decimal (art. 45, I).
            var meses = p.TotalParcelasAnosAnteriores is { } total && total > somaAnteriores
                ? Math.Max(0.1m, Math.Round(mesesTotais * somaAnteriores / total, 1, MidpointRounding.AwayFromZero))
                : mesesTotais;
            var rendimentos = somaAnteriores + correcaoAnteriores - despesasAnteriores;
            var baseCalculo = rendimentos - p.InssAnosAnteriores - p.PensaoAnosAnteriores;
            parteAnteriores = new ParteAnosAnterioresRra(somaAnteriores, correcaoAnteriores, despesasAnteriores, p.InssAnosAnteriores,
                p.PensaoAnosAnteriores, mesesTotais, anteriores.Select(item => item.Competencia.Year).Distinct().Order().ToArray(),
                tabelas.CalcularIrrfAcumulado(rendimentos, baseCalculo, meses, p.AplicarReducao));
        }

        ParteAnoPagamentoRra? parteDoAno = null;
        if (doAno.Length > 0)
        {
            var rendimentos = somaDoAno + correcaoDoAno;
            if (p.Origem == OrigemPagamentoRra.JusticaFederal)
            {
                // Precatório e RPV: 3% sobre o montante pago, sem deduções, como antecipação do ajuste anual.
                parteDoAno = new ParteAnoPagamentoRra(somaDoAno, correcaoDoAno, despesasDoAno, p.InssAnoPagamento, p.PensaoAnoPagamento,
                    null, null, CalculadoraTributacao.Arredondar(rendimentos * AliquotaJusticaFederal / 100m));
            }
            else
            {
                // Art. 12-B: a parte do ano entra no mês do recebimento, diminuída das despesas judiciais, com os demais
                // rendimentos da mesma fonte. O imposto atribuído ao RRA é o acréscimo que ela causa no IRRF do mês.
                var comOutros = tabelas.CalcularIrrf(rendimentos - despesasDoAno + p.OutrosRendimentosMes,
                    p.InssAnoPagamento + p.InssOutrosRendimentos, p.Dependentes, p.PensaoAnoPagamento);
                var soOutros = p.OutrosRendimentosMes > 0m ? tabelas.CalcularIrrf(p.OutrosRendimentosMes, p.InssOutrosRendimentos, p.Dependentes) : null;
                parteDoAno = new ParteAnoPagamentoRra(somaDoAno, correcaoDoAno, despesasDoAno, p.InssAnoPagamento, p.PensaoAnoPagamento,
                    comOutros, soOutros, Math.Max(0m, comOutros.Imposto - (soOutros?.Imposto ?? 0m)));
            }
        }

        return new ApuracaoRra(parteAnteriores, parteDoAno, p.JurosDeMora, despesasNaoDedutiveis, totalRecebido);
    }

    private static Result Validar(PagamentoRra p)
    {
        if (!Enum.IsDefined(p.Origem))
            return Erro.Validacao("Escolha quem faz o pagamento: fonte pagadora ou Justiça Federal.");
        if (p.Parcelas is null || p.Parcelas.Count is 0 or > LimiteParcelas)
            return Erro.Validacao($"Informe de 1 a {LimiteParcelas} parcelas, uma por competência a que o pagamento se refere.");

        var mesPagamento = new DateOnly(p.DataPagamento.Year, p.DataPagamento.Month, 1);
        foreach (var parcela in p.Parcelas)
        {
            if (parcela is null || !Enum.IsDefined(parcela.Tipo))
                return Erro.Validacao("Revise o tipo das parcelas: mensal ou 13º salário.");
            var referencia = $"{parcela.Competencia:MM/yyyy}";
            if (parcela.Competencia > mesPagamento && (parcela.Tipo == TipoParcelaRra.Mensal || parcela.Competencia.Year > p.DataPagamento.Year))
                return Erro.Validacao($"A parcela de {referencia} é posterior ao mês do pagamento ({mesPagamento:MM/yyyy}). O RRA só se refere a meses já vencidos.");
            if (!ValorValido(parcela.Valor))
                return Erro.Validacao($"O valor da parcela de {referencia} deve ser positivo ou zero, até R$ 1 bilhão, com no máximo dois decimais.");
        }
        if (p.Parcelas.Sum(item => item.Valor) <= 0m)
            return Erro.Validacao("Informe o valor tributável de pelo menos uma parcela.");

        decimal[] valores = [p.CorrecaoMonetaria, p.JurosDeMora, p.DespesasJudiciais, p.InssAnosAnteriores, p.PensaoAnosAnteriores,
            p.InssAnoPagamento, p.PensaoAnoPagamento, p.OutrosRendimentosMes, p.InssOutrosRendimentos];
        if (valores.Any(valor => !ValorValido(valor)) || p.TotalParcelasAnosAnteriores is { } totalInformado && !ValorValido(totalInformado))
            return Erro.Validacao("Correção, juros, despesas, INSS, pensão e rendimentos do mês devem ser positivos ou zero, até R$ 1 bilhão, com no máximo dois decimais.");
        if (p.Dependentes is < 0 or > 99)
            return Erro.Validacao("Informe de 0 a 99 dependentes.");

        var anteriores = p.Parcelas.Where(item => item.Competencia.Year < p.DataPagamento.Year).Sum(item => item.Valor);
        var temAnteriores = p.Parcelas.Any(item => item.Competencia.Year < p.DataPagamento.Year);
        var temDoAno = p.Parcelas.Any(item => item.Competencia.Year == p.DataPagamento.Year);
        if (!temAnteriores && (p.InssAnosAnteriores > 0m || p.PensaoAnosAnteriores > 0m || p.TotalParcelasAnosAnteriores > 0m))
            return Erro.Validacao($"Não há parcelas de anos anteriores a {p.DataPagamento.Year}: zere o INSS, a pensão e o total do RRA parcelado dessa parte.");
        if (!temDoAno && (p.InssAnoPagamento > 0m || p.PensaoAnoPagamento > 0m))
            return Erro.Validacao($"Não há parcelas de {p.DataPagamento.Year}: zere o INSS e a pensão do ano do pagamento.");
        if (p.TotalParcelasAnosAnteriores is { } total && total > 0m && total < anteriores)
            return Erro.Validacao("O total do RRA pago em parcelas não pode ser menor que as parcelas de anos anteriores deste pagamento. Deixe 0,00 se este é o único pagamento.");
        if (p.DespesasJudiciais > p.Parcelas.Sum(item => item.Valor) + p.CorrecaoMonetaria + p.JurosDeMora)
            return Erro.Validacao("As despesas judiciais não podem passar do total recebido.");
        if (p.Origem == OrigemPagamentoRra.JusticaFederal && (p.OutrosRendimentosMes > 0m || p.InssOutrosRendimentos > 0m))
            return Erro.Validacao("No precatório ou RPV da Justiça Federal, o RRA não se soma a rendimentos normais do mês: zere esses campos.");
        if (p.InssOutrosRendimentos > p.OutrosRendimentosMes)
            return Erro.Validacao("O INSS dos rendimentos normais do mês não pode passar desses rendimentos.");
        return Result.Ok();
    }

    private static bool ValorValido(decimal valor) => valor >= 0m && valor <= LimiteValor && decimal.Round(valor, 2) == valor;
}
