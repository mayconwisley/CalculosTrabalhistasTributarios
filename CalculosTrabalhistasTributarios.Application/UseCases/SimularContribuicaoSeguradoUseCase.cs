using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// GPS do contribuinte individual e do facultativo: valor, código, vencimento e a complementação dos planos reduzidos,
/// com o salário mínimo e o teto de cada competência do período, e os três planos lado a lado.
/// </summary>
public sealed class SimularContribuicaoSeguradoUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularContribuicaoSeguradoRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularContribuicaoSeguradoRequest r, CancellationToken cancellationToken)
    {
        var parametros = new List<ParametrosContribuicaoMes>();
        foreach (var mes in CalculadoraContribuicaoSegurado.MesesDoPeriodo(r.Competencia, r.Periodicidade))
        {
            var tabelas = await tributacaoConsulta.ObterTabelasAsync(mes, cancellationToken);
            if (tabelas.Falhou)
                return tabelas.Erro;
            var minimo = tabelas.Valor.ObterSalarioMinimo();
            if (minimo.Falhou)
                return minimo.Erro;
            parametros.Add(new ParametrosContribuicaoMes(mes, minimo.Valor, tabelas.Valor.TetoInss));
        }

        var entrada = new EntradaContribuicaoSegurado(r.Categoria, r.Plano, r.Periodicidade, r.SalarioContribuicao, parametros);
        var resultado = CalculadoraContribuicaoSegurado.Calcular(entrada);
        if (resultado.Falhou)
            return resultado.Erro;
        return Montar(resultado.Valor);
    }

    private static DemonstrativoDto Montar(ApuracaoContribuicaoSegurado c)
    {
        var e = c.Entrada;
        var trimestral = e.Periodicidade == PeriodicidadeContribuicao.Trimestral;
        var periodo = trimestral
            ? $"Trimestre {Formato.Competencia(e.Meses[0].Competencia)} a {Formato.Competencia(e.Meses[^1].Competencia)}"
            : $"Competência {Formato.Competencia(e.Meses[0].Competencia)}";
        var reduzido = e.Plano != PlanoContribuicao.Normal;
        var complementacao = Formato.PercentualCurto(c.PercentualComplementacao);

        var destaques = new List<DestaqueDto>
        {
            new("Valor da GPS", Formato.Moeda(c.Total), trimestral ? "Soma dos três meses do trimestre" : periodo),
            new("Código da GPS", c.Codigo, $"{NomeCategoria(e.Categoria)}, {NomePlano(e.Plano)}, {(trimestral ? "trimestral" : "mensal")}"),
            new("Vencimento", Formato.Data(c.Vencimento), "Dia 15 do mês seguinte; no fim de semana, a segunda-feira"),
            reduzido
                ? new("Complementação até 20%", Formato.Moeda(c.TotalComplementacao), c.CodigoComplementacao is { } codigo ? $"{complementacao} do mínimo, código {codigo}" : $"{complementacao} do mínimo, guia do Meu INSS")
                : new("Complementação até 20%", "Não se aplica", "O plano normal já conta para todos os benefícios")
        };

        var proventos = c.Meses.Select(mes => new VerbaDto($"Contribuição de {Formato.Competencia(mes.Parametros.Competencia)}",
            $"{Formato.PercentualCurto(mes.Aliquota)} de {Formato.Moeda(mes.Base)}", mes.Valor)).ToArray();
        var informativos = new List<VerbaDto>();
        if (reduzido)
            informativos.Add(new($"Complementação de {complementacao} (opcional)", c.CodigoComplementacao ?? "Guia no Meu INSS", c.TotalComplementacao));
        informativos.Add(new("Salário mínimo", Formato.Competencia(e.Meses[^1].Competencia), e.Meses[^1].SalarioMinimo));
        informativos.Add(new("Teto do INSS", Formato.Competencia(e.Meses[^1].Competencia), e.Meses[^1].Teto));

        var formulas = c.Meses.Select(mes => new FormulaDto($"Contribuição de {Formato.Competencia(mes.Parametros.Competencia)}", mes.LimitadaAoTeto
            ? $"{Formato.Moeda(e.SalarioContribuicao)}, limitado ao teto de {Formato.Moeda(mes.Base)}, x {Formato.PercentualCurto(mes.Aliquota)} = {Formato.Moeda(mes.Valor)}"
            : $"{Formato.Moeda(mes.Base)}{(reduzido ? " (salário mínimo)" : "")} x {Formato.PercentualCurto(mes.Aliquota)} = {Formato.Moeda(mes.Valor)}")).ToList();
        if (reduzido)
            formulas.AddRange(c.Meses.Select(mes => new FormulaDto($"Complementação de {Formato.Competencia(mes.Parametros.Competencia)}",
                $"{Formato.Moeda(mes.Parametros.SalarioMinimo)} x ({Formato.PercentualCurto(CalculadoraContribuicaoSegurado.AliquotaNormal)} - {Formato.PercentualCurto(mes.Aliquota)}) = {Formato.Moeda(mes.Complementacao)}")));
        formulas.Add(new("Vencimento", $"Dia 15 do mês seguinte {(trimestral ? "ao trimestre" : "à competência")}: {Formato.Data(c.Vencimento)}"));

        var observacoes = new List<string>
        {
            "Plano normal (20%): qualquer salário de contribuição entre o salário mínimo e o teto do INSS; conta para todos os benefícios, inclusive a aposentadoria por tempo de contribuição das regras de transição e a certidão de tempo de contribuição, e os benefícios são calculados sobre essa base (Lei 8.212/1991, art. 21).",
            "Plano simplificado (11% do salário mínimo): para o facultativo e para o contribuinte individual que trabalha por conta própria, sem prestar serviço a empresa (art. 21, § 2º, I). Benefícios no valor do salário mínimo; não conta para a aposentadoria por tempo de contribuição nem para a certidão de tempo de contribuição sem a complementação de 9% (§ 3º).",
            "Facultativo de baixa renda (5% do salário mínimo): para quem não tem renda própria, se dedica só ao trabalho doméstico na própria casa e tem a família inscrita no CadÚnico, com renda mensal de até dois salários mínimos (art. 21, § 2º, II, \"b\", e § 4º). Sem cadastro válido, o INSS pode não validar as contribuições.",
            "Quem presta serviço a empresa tem 11% retidos pela empresa sobre o valor pago (Lei 10.666/2003, art. 4º): use Pró-labore e autônomo. O facultativo não pode exercer atividade que o filie obrigatoriamente ao INSS.",
            "O vencimento considera só sábados e domingos; num feriado, o pagamento passa para o dia útil seguinte (Lei 8.212/1991, art. 30, II). Pago depois do vencimento, há juros e multa: use Tributo em atraso.",
            "As contribuições ao INSS pagas pelo próprio segurado são dedutíveis no carnê-leão e na declaração anual do IR."
        };

        return new DemonstrativoDto("Contribuição do autônomo e do facultativo", $"{periodo} • {NomeCategoria(e.Categoria)}",
            destaques, proventos, [], informativos, [new("Contribuição", $"GPS de {Formato.Moeda(c.Total)}", formulas)], observacoes,
            RotuloProventos: "Contribuição", RotuloResultado: "Total a pagar na GPS", Comparativo: Comparar(e));
    }

    /// <summary>Os três planos sobre a mesma competência, pela mesma regra, para comparar custo e cobertura.</summary>
    private static TabelaComparativaDto Comparar(EntradaContribuicaoSegurado e)
    {
        string Valor(PlanoContribuicao plano)
        {
            var baseNormal = e.Plano == PlanoContribuicao.Normal ? e.SalarioContribuicao : e.Meses[0].SalarioMinimo;
            var apuracao = CalculadoraContribuicaoSegurado.Calcular(e with { Plano = plano, SalarioContribuicao = baseNormal });
            return apuracao.Falhou ? "Não se aplica" : Formato.Moeda(apuracao.Valor.Total);
        }
        var autonomo = e.Categoria == CategoriaSegurado.ContribuinteIndividual;
        return new TabelaComparativaDto("Planos de contribuição", ["Normal (20%)", "Simplificado (11%)", "Baixa renda (5%)"],
        [
            new(e.Periodicidade == PeriodicidadeContribuicao.Trimestral ? "Valor do trimestre" : "Valor do mês", [Valor(PlanoContribuicao.Normal), Valor(PlanoContribuicao.Simplificado), Valor(PlanoContribuicao.BaixaRenda)], true),
            new("Base", ["Do mínimo ao teto", "Salário mínimo", "Salário mínimo"]),
            new("Aposentadoria por idade", ["Sim", "Sim", autonomo ? "Não se aplica" : "Sim"]),
            new("Tempo de contribuição e certidão", ["Sim", "Com a complementação de 9%", autonomo ? "Não se aplica" : "Com a complementação de 15%"]),
            new("Benefícios acima do mínimo", ["Sim, pela base", "Não", autonomo ? "Não se aplica" : "Não"])
        ]);
    }

    private static string NomeCategoria(CategoriaSegurado categoria) => categoria == CategoriaSegurado.ContribuinteIndividual ? "Contribuinte individual" : "Facultativo";

    private static string NomePlano(PlanoContribuicao plano) => plano switch
    {
        PlanoContribuicao.Normal => "plano normal",
        PlanoContribuicao.Simplificado => "plano simplificado",
        _ => "baixa renda"
    };
}
