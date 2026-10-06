using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Comissões, DSR e incidências mensais, com a mesma tributação usada no holerite.</summary>
public sealed class SimularComissoesUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularComissoesRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularComissoesRequest request, CancellationToken cancellationToken)
    {
        if (request.SalarioFixo < 0m || request.Comissoes <= 0m || request.Dependentes < 0 || request.PisoGarantido < 0m)
            return Erro.Validacao("Informe comissões maiores que zero; salário, garantia mínima e dependentes não podem ser negativos.");

        var calculo = CalculadoraComissoes.Calcular(new ComissoesInformadas(request.Competencia, request.Comissoes,
            request.IncluiDsr, request.Feriados, request.DescansosPerdidos, request.DiasUteis, request.DiasDescanso));
        if (calculo.Falhou)
            return calculo.Erro;
        var comissoes = calculo.Valor;

        var consulta = await tributacaoConsulta.ObterTabelasAsync(request.Competencia, cancellationToken);
        if (consulta.Falhou)
            return consulta.Erro;
        var tabelas = consulta.Valor;
        var consultaPiso = GarantiaComissionista.ObterPiso(tabelas, request.PisoGarantido, comissoes.DiasInformados);
        if (consultaPiso.Falhou)
            return consultaPiso.Erro;
        var piso = consultaPiso.Valor;
        var complemento = CalculadoraComissoes.ComplementoGarantiaMinima(request.SalarioFixo + comissoes.Total, piso);
        var remuneracao = request.SalarioFixo + comissoes.Total + complemento;
        var inss = tabelas.CalcularInss(remuneracao);
        var irrf = tabelas.CalcularIrrf(remuneracao, inss.Valor, request.Dependentes);
        var liquido = remuneracao - inss.Valor - irrf.Imposto;
        var fgts = CalculadoraTributacao.Arredondar(remuneracao * .08m);

        var baseSemComissoes = Math.Max(request.SalarioFixo, piso);
        var inssSemComissoes = tabelas.CalcularInss(baseSemComissoes);
        var irrfSemComissoes = tabelas.CalcularIrrf(baseSemComissoes, inssSemComissoes.Valor, request.Dependentes);
        var liquidoSemComissoes = baseSemComissoes - inssSemComissoes.Valor - irrfSemComissoes.Imposto;
        var impactoLiquido = liquido - liquidoSemComissoes;

        var proventos = new List<VerbaDto>();
        if (request.SalarioFixo > 0m) proventos.Add(new("Salário fixo", "", request.SalarioFixo));
        proventos.Add(new("Comissões", "", comissoes.Comissoes));
        proventos.Add(new("DSR sobre comissões", $"{comissoes.DescansosPagos} descansos", comissoes.Dsr));
        if (complemento > 0m) proventos.Add(new("Complemento da garantia mínima", "", complemento));

        var formulas = new List<FormulaDto>(DemonstrativoComissoes.Formulas(comissoes));
        formulas.Add(new("Garantia mínima", $"Máximo entre {Formato.Moeda(0m)} e {Formato.Moeda(piso)} (piso) - {Formato.Moeda(request.SalarioFixo + comissoes.Total)} (fixo, comissões e DSR) = {Formato.Moeda(complemento)}"));
        formulas.Add(new("Remuneração do mês", $"{Formato.Moeda(request.SalarioFixo)} (fixo) + {Formato.Moeda(comissoes.Total)} (comissões e DSR) + {Formato.Moeda(complemento)} (garantia) = {Formato.Moeda(remuneracao)}"));
        formulas.Add(new("Impacto líquido das comissões", $"{Formato.Moeda(liquido)} (com comissões) - {Formato.Moeda(liquidoSemComissoes)} (sem comissões, com a garantia mínima) = {Formato.Moeda(impactoLiquido)}"));

        var observacoes = new List<string>
        {
            "As comissões integram o salário e geram repouso semanal e feriados remunerados (CLT, art. 457, § 1º; Súmula 27 do TST).",
            "A garantia mensal de quem recebe remuneração variável não pode ser inferior ao salário mínimo; informe um piso maior quando a categoria exigir (Constituição, art. 7º, VII; Lei 8.716/1993).",
            "Na contagem automática, os sábados são dias úteis; informe apenas feriados que não coincidam com domingos. Para escala, período parcial ou regra coletiva diferente, informe os dias úteis e os repousos do período.",
            "O DSR das comissões é separado do DSR das horas extras. No holerite, informe as comissões sem DSR no campo próprio ou informe o total com DSR em Proventos tributáveis, nunca nos dois.",
            "O salário líquido considera somente salário fixo, comissões, DSR e eventual complemento da garantia. Horas extras sobre comissões, outros adicionais, descontos e benefícios não estão neste cenário. O FGTS é depósito do empregador e não reduz o líquido."
        };
        if (complemento > 0m)
            observacoes.Add($"A remuneração ficou abaixo da garantia de {Formato.Moeda(piso)}; o complemento de {Formato.Moeda(complemento)} entra no INSS, no IRRF e no FGTS.");
        if (request.IncluiDsr)
            observacoes.Add("O valor informado já inclui o DSR: a divisão entre comissões e repouso é uma estimativa proporcional aos dias informados e preserva o total pago.");
        if (request.DescansosPerdidos > 0)
            observacoes.Add("Os descansos perdidos foram retirados apenas do DSR das comissões. Se houver salário fixo, informe também a perda correspondente no holerite completo.");

        return new DemonstrativoDto(
            "Comissões e DSR",
            $"Competência {Formato.Competencia(tabelas.Competencia)}",
            [
                new("Comissões + DSR", Formato.Moeda(comissoes.Total), "Valor tributável para o holerite"),
                new("DSR das comissões", Formato.Moeda(comissoes.Dsr), $"{comissoes.DescansosPagos} repousos remunerados"),
                new("Impacto líquido", Formato.Moeda(impactoLiquido), "Comparado ao cenário sem comissões, com garantia mínima"),
                new("Líquido do cenário", Formato.Moeda(liquido), "Salário fixo e comissões, menos INSS e IRRF")
            ],
            proventos,
            [new("INSS", "", inss.Valor), new(MemoriaTributaria.DescricaoIrrf("IRRF", irrf), MemoriaTributaria.ReferenciaIrrf(irrf), irrf.Imposto)],
            [new("FGTS", "8%", fgts)],
            [new("Comissões e DSR", $"Total: {Formato.Moeda(comissoes.Total)}", formulas),
                MemoriaTributaria.Inss("INSS", inss, "salário fixo, comissões e DSR"),
                MemoriaTributaria.Irrf("IRRF", irrf, "salário fixo, comissões e DSR")],
            observacoes);
    }
}
