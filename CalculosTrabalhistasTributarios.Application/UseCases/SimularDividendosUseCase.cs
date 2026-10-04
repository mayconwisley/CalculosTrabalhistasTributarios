using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Retenção de 10% sobre lucros e dividendos desde 01/2026 (Lei 15.270/2025): para residente no Brasil, quando a mesma
/// empresa paga à mesma pessoa mais de R$ 50 mil no mês, sobre o total e não só sobre o excedente (Lei 9.250/1995, art.
/// 6º-A); para residente no exterior, sobre qualquer valor (Lei 9.249/1995, art. 10, § 4º).
/// </summary>
public sealed class SimularDividendosUseCase : ISimularDemonstrativoUseCase<SimularDividendosRequest>
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private const decimal LimiteMensal = 50_000.00m;
    private const decimal Aliquota = 10m;

    // O cálculo não consulta o banco: a tarefa só cumpre o contrato assíncrono da interface.
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularDividendosRequest r, CancellationToken cancellationToken) => Task.FromResult(Simular(r));

    private static Result<DemonstrativoDto> Simular(SimularDividendosRequest r)
    {
        if (r.Competencia < Inicio)
            return Erro.Validacao("A retenção sobre lucros e dividendos vale para os pagamentos desde 01/2026.");
        if (r.Valor <= 0m)
            return Erro.Validacao("Informe o total de dividendos pagos no mês.");
        if (r.ParteTransicao < 0m || r.JaRetido < 0m)
            return Erro.Validacao("Os valores não podem ser negativos.");
        if (r.ParteTransicao > r.Valor)
            return Erro.Validacao("A parte de lucros até 2025 não pode passar do total pago no mês.");

        var baseRetencao = r.Valor - r.ParteTransicao;
        var sujeito = r.Exterior || baseRetencao > LimiteMensal;
        var impostoDoMes = sujeito ? CalculadoraTributacao.Arredondar(baseRetencao * Aliquota / 100m) : 0m;
        var aReter = Math.Max(0m, impostoDoMes - r.JaRetido);

        var formulas = new List<FormulaDto>();
        if (r.ParteTransicao > 0m)
            formulas.Add(new("Base da retenção", $"{Formato.Moeda(r.Valor)} - {Formato.Moeda(r.ParteTransicao)} (lucros até 2025, aprovados até 31/12/2025) = {Formato.Moeda(baseRetencao)}"));
        formulas.Add(new("Regra", r.Exterior
            ? "Beneficiário no exterior: 10% sobre qualquer valor, sem o limite de R$ 50 mil."
            : sujeito
                ? $"{Formato.Moeda(baseRetencao)} passa de {Formato.Moeda(LimiteMensal)} no mês: 10% sobre o total, e não só sobre o excedente."
                : $"{Formato.Moeda(baseRetencao)} não passa de {Formato.Moeda(LimiteMensal)} no mês: sem retenção."));
        if (sujeito)
            formulas.Add(new("Imposto do mês", $"{Formato.Moeda(baseRetencao)} x 10% = {Formato.Moeda(impostoDoMes)}"));
        if (r.JaRetido > 0m)
            formulas.Add(new("Retenção neste pagamento", $"{Formato.Moeda(impostoDoMes)} - {Formato.Moeda(r.JaRetido)} (já retido no mês) = {Formato.Moeda(aReter)}"));

        var observacoes = new List<string>
        {
            "Os pagamentos da mesma empresa à mesma pessoa são somados no mês: a cada novo pagamento, o imposto é refeito sobre o total, e o que já foi retido é descontado. Informe o total do mês e o que já foi retido.",
            "A retenção é antecipação: na declaração, ela é compensada na tributação mínima das altas rendas e, para quem recebe até R$ 600 mil no ano, volta como restituição.",
            "Não sofrem retenção os lucros apurados até 2025 cuja distribuição foi aprovada até 31/12/2025 e que são pagos nos termos do ato de aprovação. O STF, em liminar nas ADIs 7912 e 7914, estendeu o prazo de aprovação para 31/01/2026; confira a situação atual do julgamento.",
            "Os juros sobre capital próprio seguem regra própria: IRRF de 17,5% desde 01/2026, sem o limite de R$ 50 mil (LC 224/2025)."
        };

        return new DemonstrativoDto(
            "Dividendos - IRRF mensal",
            $"Pagamentos de {Formato.Competencia(r.Competencia)}",
            [
                new("IRRF a reter", Formato.Moeda(aReter), r.JaRetido > 0m ? "Neste pagamento" : "No mês"),
                new("Dividendos no mês", Formato.Moeda(r.Valor), r.ParteTransicao > 0m ? $"{Formato.Moeda(baseRetencao)} sujeitos à retenção" : "Da mesma empresa"),
                new("Imposto do mês", Formato.Moeda(impostoDoMes), sujeito ? "10% sobre o total" : "Sem retenção"),
                new("Limite mensal", r.Exterior ? "Não se aplica" : Formato.Moeda(LimiteMensal), r.Exterior ? "Residente no exterior" : "Retenção acima dele")
            ],
            [new("Lucros e dividendos", "", r.Valor)],
            aReter > 0m ? [new("IRRF sobre lucros e dividendos", "10%", aReter)] : [],
            [new("Imposto do mês sobre os dividendos", sujeito ? "10%" : "", impostoDoMes), new("IRRF já retido no mês", "", r.JaRetido)],
            [new GrupoMemoriaDto("Retenção sobre dividendos", $"IRRF: {Formato.Moeda(aReter)}", formulas)],
            observacoes,
            RotuloResultado: "Líquido a pagar ao sócio");
    }
}
