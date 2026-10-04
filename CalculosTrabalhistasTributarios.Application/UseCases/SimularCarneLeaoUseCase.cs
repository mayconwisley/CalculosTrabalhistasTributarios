using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Carnê-leão: o IR mensal sobre rendimentos recebidos de pessoas físicas e do exterior, como honorários e aluguéis,
/// pela tabela progressiva mensal, com as deduções legais ou o desconto simplificado e a redução mensal, recolhido em
/// DARF até o último dia útil do mês seguinte (IN RFB 1.500/2014).
/// </summary>
public sealed class SimularCarneLeaoUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularCarneLeaoRequest>
{
    /// <summary>DARF de menos de R$ 10,00 não é pago: o valor passa para o mês seguinte (Lei 9.430/1996, art. 68).</summary>
    private const decimal DarfMinimo = 10m;

    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularCarneLeaoRequest r, CancellationToken cancellationToken)
    {
        if (r.Rendimentos < 0m || r.Alugueis < 0m || r.DespesasAluguel < 0m || r.LivroCaixa < 0m || r.InssPago < 0m || r.Dependentes < 0 || r.PensaoPaga < 0m)
            return Erro.Validacao("Os valores e os dependentes não podem ser negativos.");
        if (r.Rendimentos + r.Alugueis <= 0m)
            return Erro.Validacao("Informe os rendimentos ou os aluguéis recebidos no mês.");
        if (r.DespesasAluguel > r.Alugueis)
            return Erro.Validacao("As despesas do aluguel não podem passar dos aluguéis recebidos.");

        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(r.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;

        var aluguelLiquido = r.Alugueis - r.DespesasAluguel;
        var rendimentos = r.Rendimentos + aluguelLiquido;
        // O livro-caixa só abate os rendimentos do trabalho; o excesso passa para os meses seguintes do ano.
        var livroCaixa = Math.Min(r.LivroCaixa, r.Rendimentos);
        // O limite de R$ 10,00 da dispensa de retenção não vale aqui: no carnê-leão, o valor pequeno se acumula (art. 68).
        var irrf = tabelas.CalcularIrrf(rendimentos, r.InssPago, r.Dependentes, r.PensaoPaga, tributacaoExclusiva: true, livroCaixa: livroCaixa);
        var imposto = irrf.Imposto;
        var acumula = imposto > 0m && imposto < DarfMinimo;
        var vencimento = Prazos.UltimoDiaUtil(r.Competencia.AddMonths(1));

        var proventos = new List<VerbaDto>();
        if (r.Rendimentos > 0m) proventos.Add(new("Rendimentos de trabalho (pessoas físicas e exterior)", "", r.Rendimentos));
        if (r.Alugueis > 0m) proventos.Add(new("Aluguéis recebidos de pessoas físicas", "", r.Alugueis));
        var descontos = new List<VerbaDto>();
        if (r.DespesasAluguel > 0m) descontos.Add(new("IPTU, condomínio e administração do aluguel", "Fora da base", r.DespesasAluguel));
        descontos.Add(new(MemoriaTributaria.DescricaoIrrf("Carnê-leão", irrf), MemoriaTributaria.ReferenciaIrrf(irrf), imposto));

        var observacoes = new List<string>
        {
            $"Pague em DARF, código 0190, até {Formato.Data(vencimento)}, último dia útil do mês seguinte, pelo Carnê-Leão Web, no e-CAC. O imposto pago é compensado na declaração anual.",
            "Entram no carnê-leão os rendimentos de trabalho sem vínculo recebidos de pessoas físicas (como consultas e aulas particulares), os aluguéis e a pensão alimentícia recebidos de pessoas físicas e os rendimentos do exterior. O que é pago por empresa tem IRRF retido por ela e fica de fora.",
            "As deduções legais (INSS, dependentes, pensão e livro-caixa) e o desconto simplificado mensal são comparados, e vale o que resulta em imposto menor; a redução mensal da Lei 15.270/2025 é aplicada depois."
        };
        if (acumula)
            observacoes.Insert(0, $"O imposto de {Formato.Moeda(imposto)} é menor que R$ 10,00: não é pago neste mês e se soma ao do mês seguinte (Lei 9.430/1996, art. 68).");
        if (r.LivroCaixa > livroCaixa)
            observacoes.Add($"O livro-caixa de {Formato.Moeda(r.LivroCaixa)} passou dos rendimentos de trabalho: {Formato.Moeda(livroCaixa)} foram deduzidos, e o excesso pode ser usado nos meses seguintes do mesmo ano.");

        return new DemonstrativoDto(
            "Carnê-leão",
            $"Competência {Formato.Competencia(r.Competencia)} • DARF vence em {Formato.Data(vencimento)}",
            [
                new("Imposto a pagar", acumula ? "Acumula" : Formato.Moeda(imposto), acumula ? "Menor que R$ 10,00" : $"DARF 0190 até {Formato.Data(vencimento)}"),
                new("Base de cálculo", Formato.Moeda(irrf.Aplicada.BaseCalculo), irrf.SimplificadaAplicada ? "Com desconto simplificado" : "Com deduções legais"),
                new("Rendimentos tributáveis", Formato.Moeda(rendimentos), aluguelLiquido > 0m ? "Com o aluguel líquido das despesas" : "No mês"),
                new("Alíquota efetiva", rendimentos > 0m ? Formato.Percentual(Math.Round(imposto / rendimentos * 100m, 2)) : "0,00%", "Imposto sobre os rendimentos")
            ],
            proventos,
            descontos,
            [],
            [MemoriaTributaria.Irrf("Carnê-leão", irrf, aluguelLiquido > 0m && r.Rendimentos > 0m ? "rendimentos e aluguel líquido" : aluguelLiquido > 0m ? "aluguel líquido" : "rendimentos")],
            observacoes,
            RotuloResultado: "Líquido depois do imposto");
    }
}
