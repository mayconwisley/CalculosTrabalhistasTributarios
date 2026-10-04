using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Revisão de pensão alimentícia: a pensão atual e a proposta lado a lado, com o efeito de cada uma no IRRF e no líquido
/// de quem paga. Cada cenário é calculado como na calculadora de pensão, na modalidade de menor IRRF + pensão.
/// </summary>
public sealed class SimularRevisaoPensaoUseCase(ISimularPensaoUseCase simularPensao, ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularRevisaoPensaoRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularRevisaoPensaoRequest r, CancellationToken cancellationToken)
    {
        if (r.ValorBruto < 0m || r.Dependentes < 0)
            return Erro.Validacao("O valor bruto e a quantidade de dependentes não podem ser negativos.");

        var cenarioAtual = await Simular(r, r.Atual, "Pensão atual", cancellationToken);
        if (cenarioAtual.Falhou)
            return cenarioAtual.Erro;
        var cenarioProposto = await Simular(r, r.Proposta, "Pensão proposta", cancellationToken);
        if (cenarioProposto.Falhou)
            return cenarioProposto.Erro;
        var (atual, proposta) = (cenarioAtual.Valor, cenarioProposto.Valor);
        var salarioMinimo = await tributacaoConsulta.ObterSalarioMinimoAsync(r.Competencia, cancellationToken);
        var diferenca = proposta.Pensao - atual.Pensao;
        var variacao = atual.Pensao > 0m ? diferenca / atual.Pensao * 100m : (decimal?)null;

        var linhas = new List<LinhaComparativaDto>
        {
            new("Regra", [atual.Regra, proposta.Regra, ""]),
            new("Pensão por mês", [Formato.Moeda(atual.Pensao), Formato.Moeda(proposta.Pensao), ComSinal(diferenca)], Destaque: true),
            new("Pensão em 12 meses", [Formato.Moeda(atual.Pensao * 12m), Formato.Moeda(proposta.Pensao * 12m), ComSinal(diferenca * 12m)]),
            new("Parte do líquido de quem paga", [Formato.Percentual(atual.ParteDoLiquido), Formato.Percentual(proposta.ParteDoLiquido), PontosPercentuais(proposta.ParteDoLiquido - atual.ParteDoLiquido)])
        };
        if (salarioMinimo is { } minimo && minimo > 0m)
            linhas.Add(new("Em salários mínimos", [SalariosMinimos(atual.Pensao, minimo), SalariosMinimos(proposta.Pensao, minimo), SalariosMinimos(diferenca, minimo, comSinal: true)]));
        linhas.Add(new("IRRF de quem paga", [Formato.Moeda(atual.Imposto), Formato.Moeda(proposta.Imposto), ComSinal(proposta.Imposto - atual.Imposto)]));
        linhas.Add(new("IRRF + pensão", [Formato.Moeda(atual.Imposto + atual.Pensao), Formato.Moeda(proposta.Imposto + proposta.Pensao), ComSinal(proposta.Imposto + proposta.Pensao - atual.Imposto - atual.Pensao)]));
        linhas.Add(new("Líquido de quem paga", [Formato.Moeda(atual.Liquido), Formato.Moeda(proposta.Liquido), ComSinal(proposta.Liquido - atual.Liquido)], Destaque: true));

        return new DemonstrativoDto(
            "Revisão de pensão alimentícia",
            $"Competência {Formato.Competencia(r.Competencia)} • valor bruto de {Formato.Moeda(r.ValorBruto)}",
            [
                new("Pensão atual", Formato.Moeda(atual.Pensao), atual.Regra),
                new("Pensão proposta", Formato.Moeda(proposta.Pensao), proposta.Regra),
                new("Diferença por mês", ComSinal(diferenca), variacao is { } percentual
                    ? $"{(percentual > 0m ? "+" : "")}{Formato.Percentual(Math.Round(percentual, 2))} sobre a pensão atual"
                    : "A pensão atual é zero"),
                new("Líquido de quem paga", Formato.Moeda(proposta.Liquido), $"Com a pensão proposta; hoje, {Formato.Moeda(atual.Liquido)}")
            ],
            [],
            [],
            [],
            [atual.Memoria, proposta.Memoria],
            [
                "Valores mensais. Em cada cenário, o IRRF é o da modalidade de menor IRRF + pensão, a que a fonte pagadora aplica: a pensão é deduzida da base nas deduções legais, e não no desconto simplificado.",
                "Parte do líquido de quem paga: pensão ÷ (valor bruto − INSS − IRRF do cenário).",
                "A base de INSS considerada é o valor bruto. A pensão sobre o 13º salário e as férias depende do que a decisão determinar e não está no total de 12 meses."
            ],
            Comparativo: new TabelaComparativaDto("Comparação", ["Atual", "Proposta", "Diferença"], linhas));
    }

    private async Task<Result<Cenario>> Simular(SimularRevisaoPensaoRequest r, RegraPensao regra, string titulo, CancellationToken cancellationToken)
    {
        var entrada = new EntradaPensaoDto(r.Competencia, r.ValorBruto, r.ValorBruto, r.Dependentes, regra, 0m);
        var resultado = await simularPensao.ExecutarAsync(new SimularPensaoRequest(r.Competencia, r.ValorBruto, r.ValorBruto, r.Dependentes, regra, 0m), cancellationToken);
        if (resultado.Falhou)
            return resultado.Erro;
        var simulacao = resultado.Valor;
        var aplicada = simulacao.Aplicada;
        var ultima = aplicada.Detalhes[^1];
        var liquidoAntesDaPensao = r.ValorBruto - simulacao.ValorInss - aplicada.Imposto;
        var formulas = new List<FormulaDto> { new("Como foi calculada", MemoriaCalculoPensao.Explicacao(simulacao, entrada, Formato.Moeda, Formato.Percentual)) };
        formulas.AddRange(MemoriaCalculoPensao.Iteracao(aplicada, ultima, entrada, simulacao.ValorInss, Formato.Moeda, Formato.Percentual));
        formulas.Add(new("Líquido de quem paga", $"{Formato.Moeda(r.ValorBruto)} - {Formato.Moeda(simulacao.ValorInss)} (INSS) - {Formato.Moeda(aplicada.Imposto)} (IRRF) - {Formato.Moeda(aplicada.Pensao)} (pensão) = {Formato.Moeda(liquidoAntesDaPensao - aplicada.Pensao)}"));
        return new Cenario(
            ultima.Beneficiarios[0].Descrever(Formato.Moeda, Formato.Percentual),
            aplicada.Pensao,
            aplicada.Imposto,
            liquidoAntesDaPensao - aplicada.Pensao,
            liquidoAntesDaPensao > 0m ? Math.Round(aplicada.Pensao / liquidoAntesDaPensao * 100m, 2) : 0m,
            new GrupoMemoriaDto(titulo, $"Pensão: {Formato.Moeda(aplicada.Pensao)}", formulas));
    }

    private static string ComSinal(decimal valor) => Formato.MoedaComSinal(valor);

    private static string PontosPercentuais(decimal valor) => $"{(valor > 0m ? "+" : valor < 0m ? "-" : "")}{Math.Abs(valor).ToString("N2", Formato.Cultura)} p.p.";

    private static string SalariosMinimos(decimal valor, decimal salarioMinimo, bool comSinal = false)
    {
        var quantidade = Math.Round(valor / salarioMinimo, 2);
        return $"{(comSinal && quantidade > 0m ? "+" : "")}{quantidade.ToString("N2", Formato.Cultura)} SM";
    }

    /// <param name="ParteDoLiquido">Pensão como percentual do líquido de quem paga antes dela.</param>
    private sealed record Cenario(string Regra, decimal Pensao, decimal Imposto, decimal Liquido, decimal ParteDoLiquido, GrupoMemoriaDto Memoria);
}
