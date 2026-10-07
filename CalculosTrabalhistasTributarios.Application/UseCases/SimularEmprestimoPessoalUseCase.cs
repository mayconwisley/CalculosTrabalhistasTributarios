using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Financeiro;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

public sealed class SimularEmprestimoPessoalUseCase : ISimularDemonstrativoUseCase<SimularEmprestimoPessoalRequest>
{
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularEmprestimoPessoalRequest r, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Montar(r.Emprestimo));
    }

    private static Result<DemonstrativoDto> Montar(EntradaEmprestimoPessoal e)
    {
        var resultado = CalculadoraEmprestimoPessoal.Calcular(e);
        if (resultado.Falhou) return resultado.Erro;
        var a = resultado.Valor;
        static string Percentual(decimal? v) => v is { } p ? p.ToString("N4", Formato.Cultura) + "%" : "Não calculado";
        var descontos = new List<VerbaDto>();
        if (e.CustosNaLiberacao > 0m) descontos.Add(new("Custos descontados na liberação", "Valor informado", e.CustosNaLiberacao));
        if (!e.FinanciarIof) descontos.Add(new("IOF descontado na liberação", "Retido do valor solicitado", a.Iof));
        return new DemonstrativoDto("Empréstimo pessoal", $"Liberação {e.DataLiberacao:dd/MM/yyyy} • {e.NumeroParcelas} parcelas mensais",
            [new("Parcela mensal", Formato.Moeda(a.Parcela), $"Última: {Formato.Moeda(a.UltimaParcela)}"),
             new("Crédito líquido", Formato.Moeda(a.CreditoLiquido), "Valor disponível na liberação"),
             new("Total a pagar", Formato.Moeda(a.TotalPago), $"Juros: {Formato.Moeda(a.TotalJuros)}"),
             new("IOF", Formato.Moeda(a.Iof), e.FinanciarIof ? "Incluído no financiamento" : "Descontado na liberação"),
             new("Custo efetivo estimado", Percentual(a.CustoEfetivoMensal) + " a.m.", Percentual(a.CustoEfetivoAnual) + " a.a.; períodos mensais"),
             new("Primeira parcela", a.PrimeiroVencimento.ToString("dd/MM/yyyy"), $"Última: {a.UltimoVencimento:dd/MM/yyyy}")],
            [new("Valor solicitado", "Antes dos descontos na liberação", e.ValorSolicitado)], descontos,
            [new("Principal financiado", "Solicitado + custos financiados", a.PrincipalFinanciado),
             new("IOF da operação", e.IofAutomatico ? "Estimativa automática PF" : "Informado pelo banco", a.Iof),
             new("Seguro financiado", "Custo informado; não é percentual de cobertura", e.SeguroFinanciado),
             new("Outros custos financiados", "Exceto IOF e seguro", e.OutrosCustosFinanciados),
             new("Juros totais", "Prestações menos principal", a.TotalJuros),
             new("Custo total sobre crédito líquido", "Prestações menos valor recebido", a.CustoTotal)],
            [new GrupoMemoriaDto("Financiamento", $"Total: {Formato.Moeda(a.TotalPago)}", [
                new("Principal", $"{Formato.Moeda(e.ValorSolicitado)} + seguro {Formato.Moeda(e.SeguroFinanciado)} + outros custos {Formato.Moeda(e.OutrosCustosFinanciados)} + IOF financiado {Formato.Moeda(e.FinanciarIof ? a.Iof : 0m)} = {Formato.Moeda(a.PrincipalFinanciado)}"),
                new("Crédito líquido", $"Solicitado {Formato.Moeda(e.ValorSolicitado)} - custos na liberação {Formato.Moeda(e.CustosNaLiberacao)} - IOF retido {Formato.Moeda(e.FinanciarIof ? 0m : a.Iof)} = {Formato.Moeda(a.CreditoLiquido)}"),
                new("Price", $"P × i × (1+i)^n / ((1+i)^n - 1); juros zero: P/n. P = {Formato.Moeda(a.PrincipalFinanciado)}; i = {e.JurosMensais.ToString("N6", Formato.Cultura)}% a.m.; n = {e.NumeroParcelas}. Juros arredondados a centavos em cada mês. Parcela {Formato.Moeda(a.Parcela)}; última ajustada {Formato.Moeda(a.UltimaParcela)}."),
                new("Totais", $"Soma das prestações = {Formato.Moeda(a.TotalPago)}; menos principal = juros {Formato.Moeda(a.TotalJuros)}; menos crédito líquido = custo total {Formato.Moeda(a.CustoTotal)}."),
                new("Taxa anual equivalente", $"(1 + taxa mensal/100)^12 - 1 = {Percentual(a.JurosAnuais)} a.a.; não é CET."),
                new("Custo efetivo", $"Taxa que iguala o valor líquido recebido aos pagamentos mensais, considerando a última parcela ajustada: {Percentual(a.CustoEfetivoMensal)} a.m.; equivalente anual {Percentual(a.CustoEfetivoAnual)}. Não substitui o CET contratual por datas.")]),
             new GrupoMemoriaDto("IOF e calendário", Formato.Moeda(a.Iof), [
                new("Vencimentos", $"Primeiro um mês após liberação: {a.PrimeiroVencimento:dd/MM/yyyy}; último: {a.UltimoVencimento:dd/MM/yyyy}. Demais datas ancoradas no primeiro vencimento, limitadas ao último dia de meses menores; sem ajuste por feriados."),
                new("IOF", e.IofAutomatico
                    ? $"Decreto 6.306/2007, art. 7º, I, b, 2, §§ 1º e 15. IOF = principal × 0,0038 + soma(amortização × mínimo(dias desde liberação, 365)) × 0,000082. Base-dias = {a.BaseDiasIof.ToString("N6", Formato.Cultura)} R$-dias; amortizações teóricas Price sem arredondamento intermediário. Resultado arredondado: {Formato.Moeda(a.Iof)}."
                    : $"Valor informado pelo banco: {Formato.Moeda(a.Iof)}; sem estimativa automática."),
                new("Forma de cobrança", e.FinanciarIof
                    ? "IOF somado ao principal. No automático: principal = (solicitado + seguro + outros custos)/(1 - coeficiente IOF); imposto arredondado em centavos."
                    : "IOF calculado sobre o principal e descontado do solicitado; não aumenta o saldo financiado.")])],
            ["Simulação de nova operação de empréstimo pessoal PF no Brasil, sem consignação. Não utiliza margem de 35%, salário ou FGTS e não assegura aprovação.",
             "Sistema Price mensal sem carência: primeira parcela um mês após a liberação. Não reproduz juros diários, juros de acerto, atraso, refinanciamento, portabilidade, isenções ou contratos com calendário diferente.",
             "IOF automático usa 0,38% adicional e 0,0082% ao dia, até 365 dias por amortização. Datas futuras mantêm essas alíquotas; alterações legais exigem revisão. Arredondamentos e calendário do banco podem gerar diferenças.",
             "Seguro é opcional nesta simulação: informe o preço em reais, não o percentual segurado. Não repita custos financiados nos descontos da liberação.",
             "Custo efetivo estimado em períodos mensais iguais; compare com o CET contratual calculado por datas, conforme Resolução CMN 4.881/2020. Os limites de valores, prazo e taxa desta ferramenta são técnicos."],
            RotuloProventos: "Crédito solicitado", RotuloResultado: "Crédito líquido na liberação");
    }
}
