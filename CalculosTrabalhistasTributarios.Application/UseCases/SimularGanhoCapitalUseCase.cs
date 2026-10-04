using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Imposto de renda sobre o ganho de capital na venda de imóveis e outros bens pela pessoa física.</summary>
public sealed class SimularGanhoCapitalUseCase : ISimularDemonstrativoUseCase<SimularGanhoCapitalRequest>
{
    // O cálculo não consulta o banco: a tarefa só cumpre o contrato assíncrono da interface.
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularGanhoCapitalRequest r, CancellationToken cancellationToken) => Task.FromResult(Simular(r));

    private static Result<DemonstrativoDto> Simular(SimularGanhoCapitalRequest r)
    {
        var alienacao = new AlienacaoDeBem(r.Bem, r.Aquisicao, r.Alienacao, r.Custo, r.Venda, r.DespesasVenda, r.UnicoImovel, r.Reinvestido, r.VendasNoMes);
        var calculo = CalculadoraGanhoDeCapital.Calcular(alienacao);
        if (calculo.Falhou)
            return calculo.Erro;
        var g = calculo.Valor;
        var vencimento = Prazos.UltimoDiaUtil(new DateOnly(r.Alienacao.Year, r.Alienacao.Month, 1).AddMonths(1));

        var formulas = new List<FormulaDto>
        {
            new("Ganho apurado", $"{Formato.Moeda(r.Venda)} (venda) - {Formato.Moeda(r.DespesasVenda)} (despesas da venda) - {Formato.Moeda(r.Custo)} (custo) = {Formato.Moeda(g.Ganho)}")
        };
        if (g.Isencao is { } isencao)
            formulas.Add(new("Isenção", isencao));
        else
        {
            if (g.PercentualReducao1988 > 0m)
                formulas.Add(new("Redução do imóvel comprado até 1988", $"Compra em {r.Aquisicao.Year}: {Formato.PercentualCurto(g.PercentualReducao1988)} do ganho (Lei 7.713/1988, art. 18)"));
            if (g.MesesFr1 > 0)
                formulas.Add(new("Fator de redução FR1", $"1 ÷ 1,0060 elevado a {g.MesesFr1} meses (de 01/1996, ou da compra, a 11/2005) = {g.Fr1.ToString("0.000000", Formato.Cultura)}"));
            if (g.MesesFr2 > 0)
                formulas.Add(new("Fator de redução FR2", $"1 ÷ 1,0035 elevado a {g.MesesFr2} meses (de 12/2005, ou da compra, até a venda) = {g.Fr2.ToString("0.000000", Formato.Cultura)}"));
            if (g.ProporcaoReinvestida > 0m)
                formulas.Add(new("Reinvestimento em imóvel residencial", $"{Formato.Moeda(Math.Min(r.Reinvestido, r.Venda))} de {Formato.Moeda(r.Venda)} aplicados em 180 dias: {Formato.Percentual(Math.Round(g.ProporcaoReinvestida * 100m, 2))} do ganho fica isento (Lei 11.196/2005, art. 39)"));
            formulas.Add(new("Ganho tributável", Formato.Moeda(g.GanhoTributavel)));
            formulas.AddRange(g.Faixas.Select((faixa, indice) => new FormulaDto($"Faixa {indice + 1}", $"{Formato.Moeda(faixa.Parcela)} x {Formato.Percentual(faixa.Aliquota)} = {Formato.Moeda(faixa.Imposto)}")));
        }

        var descontos = new List<VerbaDto>();
        if (r.DespesasVenda > 0m) descontos.Add(new("Corretagem e despesas da venda", "", r.DespesasVenda));
        descontos.Add(new("Imposto sobre o ganho de capital (DARF 4600)", g.Faixas.Count == 1 ? Formato.Percentual(g.Faixas[0].Aliquota) : "", g.Imposto));

        var observacoes = new List<string>
        {
            $"O imposto é pago em DARF, código 4600, até {Formato.Data(vencimento)}, último dia útil do mês seguinte ao da venda, e o ganho é informado no programa GCAP da Receita Federal, que gera o demonstrativo para a declaração anual.",
            "Alíquotas progressivas: 15% até R$ 5 milhões de ganho, 17,5% até R$ 10 milhões, 20% até R$ 30 milhões e 22,5% acima (Lei 8.981/1995, art. 21).",
            "Nas vendas a prazo, o imposto é pago proporcionalmente a cada parcela recebida. Ações vendidas em bolsa têm regras próprias, de renda variável, e não entram aqui."
        };
        if (alienacao.Imovel)
            observacoes.Add("Os fatores FR1 e FR2 reduzem o ganho dos imóveis pelo tempo de posse (Lei 11.196/2005, art. 40); os meses são contados entre o mês de início e o da venda. Confira o resultado no GCAP, que é a referência da Receita Federal.");

        return new DemonstrativoDto(
            "Ganho de capital",
            $"Venda em {Formato.Data(r.Alienacao)} • {NomeBem(r.Bem)}",
            [
                new("Imposto a pagar", Formato.Moeda(g.Imposto), g.Isencao is null ? $"DARF 4600 até {Formato.Data(vencimento)}" : "Isento"),
                new("Ganho apurado", Formato.Moeda(g.Ganho), "Venda menos despesas e custo"),
                new("Ganho tributável", Formato.Moeda(g.GanhoTributavel), g.Isencao is null ? "Depois das reduções" : "Sem imposto"),
                new("Alíquota efetiva", g.Ganho > 0m ? Formato.Percentual(Math.Round(g.Imposto / g.Ganho * 100m, 2)) : "0,00%", "Imposto sobre o ganho apurado")
            ],
            [new("Valor da venda", "", r.Venda)],
            descontos,
            [],
            [new GrupoMemoriaDto("Ganho de capital", $"Imposto: {Formato.Moeda(g.Imposto)}", formulas)],
            observacoes,
            RotuloProventos: "Venda",
            RotuloResultado: "Valor líquido da venda");
    }

    private static string NomeBem(BemAlienado bem) => bem switch
    {
        BemAlienado.ImovelResidencial => "Imóvel residencial",
        BemAlienado.OutroImovel => "Outro imóvel",
        _ => "Outro bem"
    };
}
