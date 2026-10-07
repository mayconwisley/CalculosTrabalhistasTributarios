using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

public sealed class SimularCreditoTrabalhadorUseCase(ITributacaoConsulta consulta) : ISimularDemonstrativoUseCase<SimularCreditoTrabalhadorRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularCreditoTrabalhadorRequest r, CancellationToken cancellationToken)
    {
        if (r.Competencia.Day != 1 || r.Competencia < new DateOnly(2025, 3, 1) || r.Competencia.Year > 2100)
            return Erro.Validacao("Competência: informe MM/AAAA entre 03/2025 e 12/2100.");
        if (r.RemuneracaoHabitual <= 0m || r.Dependentes is < 0 or > 100 ||
            new[] { r.RemuneracaoHabitual, r.DescontosPrevidenciarios, r.Pensao, r.OutrosDescontosCompulsorios }
                .Any(v => v < 0m || v > 10_000_000m || decimal.Round(v, 2) != v) || r.DescontosPrevidenciarios >= r.RemuneracaoHabitual)
            return Erro.Validacao("Informe remuneração habitual positiva, descontos não negativos inferiores à remuneração e até 100 dependentes. Dinheiro: até R$ 10 milhões e duas casas decimais.");
        var tabelas = await consulta.ObterTabelasAsync(r.Competencia, cancellationToken);
        if (tabelas.Falhou) return tabelas.Erro;
        var baseTributavel = r.RemuneracaoHabitual - r.DescontosPrevidenciarios;
        var inss = tabelas.Valor.CalcularInss(baseTributavel);
        var ir = tabelas.Valor.CalcularIrrf(baseTributavel, inss.Valor, r.Dependentes, r.Pensao);
        var disponivel = baseTributavel - inss.Valor - ir.Imposto - r.Pensao - r.OutrosDescontosCompulsorios;
        if (disponivel < 0m)
            return Erro.Validacao("Os descontos obrigatórios superam a remuneração habitual. Confira os valores; não repita INSS ou IRRF nos outros descontos.");
        var entrada = r.Credito with { RemuneracaoDisponivel = disponivel };
        var resultado = CalculadoraCreditoTrabalhador.Calcular(entrada);
        if (resultado.Falhou) return resultado.Erro;
        var a = resultado.Valor;
        var observacoes = new List<string>
        {
            "Simulação de consignado para trabalhador com vínculo formal elegível. A margem estimada é 35% da remuneração habitual disponível; verbas variáveis não foram incluídas. A margem e a elegibilidade finais são confirmadas na CTPS Digital/Dataprev e pelo banco.",
            "INSS e IRRF foram estimados somente sobre a remuneração habitual informada, usando as tabelas da competência. O holerite completo pode ter outras bases e descontos; se houver margem livre oficial, informe-a para usar o valor confirmado.",
            "Parcelas mensais pelo sistema Price, sem carência e com primeira prestação após um mês. Juros arredondados a centavos em cada mês; a última parcela ajusta o saldo residual. Prazos, taxas e liberação dependem do banco. O limite de 120 parcelas é técnico, não regra de contratação.",
            entrada.IofAutomatico
                ? "IOF automático estimado para nova operação de crédito PF: adicional de 0,38% e diário de 0,0082%, limitado a 365 dias por amortização. Imposto financiado incluído no principal. Datas afetam somente o IOF; juros continuam mensais. Não simula refinanciamento, portabilidade ou isenções."
                : "IOF financiado informado pelo banco. Não repita custos financiados nos custos descontados na liberação. Seguros e demais custos são opcionais e não presumidos.",
            "O custo efetivo estimado considera o crédito líquido e pagamentos em intervalos mensais iguais. Não é o CET contratual por datas: compare com o CET informado pelo banco, conforme Resolução CMN 4.881/2020.",
            "Garantia de FGTS é facultativa e depende de autorização, modalidade e regras vigentes. Saldo FGTS ou garantia não foi somado ao crédito nem usado para assegurar aprovação. Na perda do vínculo, a dívida não desaparece; consulte as condições do contrato para garantia, redirecionamento ou pagamento.",
            "Parcelas já existentes ocupam a margem estimada. Portabilidade, refinanciamento, vínculo elegível e contratos legados exigem análise própria; o resultado não aprova uma segunda contratação."
        };
        if (!a.CabeNaMargem) observacoes.Insert(0, "A maior prestação supera a margem livre informada/estimada. A proposta não cabe nessa margem; reduza o crédito ou reveja prazo e custos.");
        if (entrada.MargemLivreOficial is not null) observacoes.Insert(0, "Foi usada a margem livre oficial informada, já após os contratos existentes; esses contratos não foram descontados novamente desse valor.");
        var cet = a.CustoEfetivoMensalEstimado;
        var cetAnual = cet is { } c ? (CalculadoraCreditoTrabalhador.Potencia(1m + c / 100m, 12) - 1m) * 100m : (decimal?)null;
        return new DemonstrativoDto("Crédito do Trabalhador", $"Competência {r.Competencia:MM/yyyy} • {entrada.NumeroParcelas} parcelas mensais",
            [new("Parcela mensal", Formato.Moeda(a.Parcela), $"Última: {Formato.Moeda(a.UltimaParcela)}"),
             new("Margem livre", Formato.Moeda(a.MargemLivre), a.CabeNaMargem ? "Cabe na margem; sujeito ao banco" : "Prestação acima da margem"),
             new("Crédito líquido", Formato.Moeda(a.CreditoLiquido), "Após custos da liberação"),
             new("Total das prestações", Formato.Moeda(a.TotalPago), $"Juros: {Formato.Moeda(a.TotalJuros)}"),
             new("Custo efetivo estimado", cet is { } m ? $"{m.ToString("N4", Formato.Cultura)}% a.m." : "Não calculado", cetAnual is { } an ? $"{an.ToString("N4", Formato.Cultura)}% a.a.; intervalos mensais" : "Fluxo fora do limite de apuração"),
             new("Renda após descontos", Formato.Moeda(disponivel - entrada.ParcelasExistentes - a.Parcela), "Só remuneração e descontos informados")],
            [new("Crédito antes dos custos da liberação", "Valor simulado", a.ValorCredito)],
            entrada.CustosDescontadosNaLiberacao > 0m ? [new("Custos descontados na liberação", "Conforme proposta", entrada.CustosDescontadosNaLiberacao)] : [],
            [new("Principal financiado", "Crédito + IOF + custos financiados", a.PrincipalFinanciado),
             new("IOF financiado", entrada.IofAutomatico ? "Automático estimado; incluído no principal" : "Valor informado na proposta", a.IofFinanciado),
             new("Outros custos financiados", "Valor informado na proposta", entrada.OutrosCustosFinanciados),
             new("Juros das prestações", "Total pago menos principal financiado", a.TotalJuros),
             new("Custo total sobre crédito líquido", "Prestações menos crédito líquido", a.CustoTotal)],
            [MemoriaTributaria.Inss("INSS estimado da remuneração habitual", inss, "remuneração habitual após descontos previdenciários"),
             MemoriaTributaria.Irrf("IRRF estimado da remuneração habitual", ir, "remuneração habitual após descontos previdenciários"),
             new GrupoMemoriaDto("Margem consignável", $"Livre: {Formato.Moeda(a.MargemLivre)}", [
                new("Remuneração disponível", $"{Formato.Moeda(r.RemuneracaoHabitual)} - descontos previdenciários {Formato.Moeda(r.DescontosPrevidenciarios)} - INSS {Formato.Moeda(inss.Valor)} - IRRF {Formato.Moeda(ir.Imposto)} - pensão {Formato.Moeda(r.Pensao)} - compulsórios {Formato.Moeda(r.OutrosDescontosCompulsorios)} = {Formato.Moeda(disponivel)}"),
                new("Margem estimada", $"{Formato.Moeda(disponivel)} × 35% = {Formato.Moeda(a.MargemTotal)}; truncada em centavos para não exceder o limite"),
                new("Margem livre usada", entrada.MargemLivreOficial is null ? $"Máximo de zero e ({Formato.Moeda(a.MargemTotal)} - contratos existentes {Formato.Moeda(entrada.ParcelasExistentes)}) = {Formato.Moeda(a.MargemLivre)}" : $"Margem livre oficial informada: {Formato.Moeda(a.MargemLivre)}; substitui a estimativa")]),
             new GrupoMemoriaDto("Financiamento e custos", $"Total pago: {Formato.Moeda(a.TotalPago)}", [
                new("Principal", $"{Formato.Moeda(a.ValorCredito)} + IOF {Formato.Moeda(a.IofFinanciado)} + outros custos {Formato.Moeda(entrada.OutrosCustosFinanciados)} = {Formato.Moeda(a.PrincipalFinanciado)}"),
                new("IOF", entrada.IofAutomatico
                    ? $"Liberação {entrada.DataLiberacao:dd/MM/yyyy}; primeiro vencimento {entrada.PrimeiroVencimento:dd/MM/yyyy}; último {entrada.PrimeiroVencimento?.AddMonths(entrada.NumeroParcelas - 1):dd/MM/yyyy}. Vencimentos mensais ancorados no dia do primeiro, limitados ao último dia de meses menores; sem ajuste de feriados. Amortizações teóricas Price sem arredondamento intermediário. Soma(amortização × mínimo(dias corridos, 365)) = {a.BaseDiasIof.ToString("N6", Formato.Cultura)} R$-dias. IOF = principal × 0,0038 + base-dias × 0,000082; arredondado no total: {Formato.Moeda(a.IofFinanciado)}. No valor desejado, principal = (crédito + outros custos)/(1 - coeficiente IOF); na margem, IOF é deduzido do principal financiável. Decreto 6.306/2007, art. 7º, I, b, 2, §§ 1º e 15. Datas sugeridas devem ser conferidas; arredondamentos e calendário do banco podem diferir."
                    : $"IOF da proposta: {Formato.Moeda(a.IofFinanciado)}; sem cálculo automático."),
                new("Price", $"P × i × (1+i)^n / ((1+i)^n - 1); com juros zero: P/n. i = {entrada.JurosMensais.ToString("N6", Formato.Cultura)}% a.m.; n = {entrada.NumeroParcelas}. Parcela {Formato.Moeda(a.Parcela)}; última {Formato.Moeda(a.UltimaParcela)}"),
                new("Modo pela margem", entrada.Objetivo == ObjetivoCreditoTrabalhador.LimitePelaMargem ? "Saldo reconstruído de trás para frente com a parcela limitada à margem e juros arredondados mensalmente." : "Valor desejado informado; margem usada para conferir a maior parcela."),
                new("Juros e custo", $"Total {Formato.Moeda(a.TotalPago)} - principal {Formato.Moeda(a.PrincipalFinanciado)} = juros {Formato.Moeda(a.TotalJuros)}. Total - crédito líquido {Formato.Moeda(a.CreditoLiquido)} = custo {Formato.Moeda(a.CustoTotal)}"),
                new("Taxa anual equivalente", $"(1 + {entrada.JurosMensais.ToString("N6", Formato.Cultura)}/100)^12 - 1 = {a.JurosAnuais.ToString("N6", Formato.Cultura)}% a.a.; não é CET"),
                new("Custo efetivo estimado", "Taxa que iguala o crédito líquido à soma das prestações descontadas por períodos mensais iguais, incluindo ajuste da última. CET oficial depende das datas e de todos os custos da proposta.")])],
            observacoes, RotuloProventos: "Crédito simulado", RotuloResultado: "Crédito líquido na liberação");
    }
}
