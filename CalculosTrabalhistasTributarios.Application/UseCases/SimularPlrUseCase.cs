using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Participação nos lucros ou resultados (Lei 10.101/2000): IRRF exclusivo na fonte pela tabela anual, sobre o total pago
/// no ano, descontado o imposto já retido. Não há INSS nem FGTS, e só a pensão alimentícia é deduzida da base.
/// </summary>
public sealed class SimularPlrUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularPlrRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularPlrRequest r, CancellationToken cancellationToken)
    {
        if (r.Valor < 0m || r.PlrAnterior < 0m || r.ImpostoRetidoAnterior < 0m || r.Pensao is { EhPercentual: false, Valor: < 0m })
            return Erro.Validacao("Os valores da PLR, do imposto retido e da pensão não podem ser negativos.");
        if (r.Pensao is { EhPercentual: false } informada && informada.Valor > r.Valor)
            return Erro.Validacao("A pensão alimentícia não pode ser maior que a PLR paga.");

        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(r.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        var totalAno = r.PlrAnterior + r.Valor;
        var semPensao = tabelas.CalcularIrrfPlr(totalAno);
        if (semPensao.Falhou)
            return semPensao.Erro;

        // A pensão desta parcela reduz a base do imposto do ano; na base líquida, ela incide sobre a PLR menos o IRRF desta parcela.
        // Com a tabela da PLR cadastrada, as apurações seguintes não falham: só a base muda.
        ApuracaoPlr Apurar(decimal pensao) => tabelas.CalcularIrrfPlr(Math.Max(0m, totalAno - pensao)).Valor;
        decimal ImpostoDaParcela(ApuracaoPlr apuracaoAno) => Math.Max(0m, apuracaoAno.Imposto - r.ImpostoRetidoAnterior);
        var resultadoPensao = r.Pensao is { } regra ? CalculadoraPensao.Calcular(regra, r.Valor, 0m, Apurar, ImpostoDaParcela) : null;
        if (resultadoPensao is { Falhou: true })
            return resultadoPensao.Erro;
        var calculoPensao = resultadoPensao?.Valor;
        var apuracao = calculoPensao?.Apuracao ?? semPensao.Valor;
        var pensaoAlimenticia = calculoPensao?.Pensao ?? 0m;
        var imposto = ImpostoDaParcela(apuracao);
        var liquido = r.Valor - imposto - pensaoAlimenticia;
        var isencao = tabelas.FaixasPlr.OrderBy(faixa => faixa.Numero).First().Limite;
        var temAnterior = r.PlrAnterior > 0m || r.ImpostoRetidoAnterior > 0m;

        var descontos = new List<VerbaDto> { new("IRRF sobre a PLR", imposto > 0m ? Formato.PercentualCurto(apuracao.Aliquota) : "", imposto) };
        if (pensaoAlimenticia > 0m) descontos.Add(new("Pensão alimentícia sobre a PLR", r.Pensao is { } regraPensao ? DemonstrativoPensao.Referencia(regraPensao) : "", pensaoAlimenticia));

        var informativos = new List<VerbaDto>();
        if (temAnterior)
        {
            informativos.Add(new("PLR acumulada no ano", "", totalAno));
            informativos.Add(new("IRRF do ano pela tabela da PLR", Formato.PercentualCurto(apuracao.Aliquota), apuracao.Imposto));
            informativos.Add(new("IRRF já retido na PLR anterior", "", r.ImpostoRetidoAnterior));
        }

        var formulas = new List<FormulaDto>();
        if (temAnterior)
            formulas.Add(new("PLR do ano", $"{Formato.Moeda(r.PlrAnterior)} (anterior) + {Formato.Moeda(r.Valor)} (atual) = {Formato.Moeda(totalAno)}"));
        formulas.Add(new("Base de cálculo", pensaoAlimenticia > 0m
            ? $"{Formato.Moeda(totalAno)} (PLR) - {Formato.Moeda(pensaoAlimenticia)} (pensão alimentícia) = {Formato.Moeda(apuracao.BaseCalculo)}"
            : $"{Formato.Moeda(apuracao.BaseCalculo)} (PLR, sem deduções)"));
        formulas.Add(new("Imposto pela tabela anual", apuracao.Aliquota == 0m
            ? $"{Formato.Moeda(apuracao.BaseCalculo)} está na faixa isenta, até {Formato.Moeda(isencao)}: {Formato.Moeda(0m)}"
            : $"{Formato.Moeda(apuracao.BaseCalculo)} x {Formato.Percentual(apuracao.Aliquota)} - {Formato.Moeda(apuracao.Deducao)} = {Formato.Moeda(apuracao.Imposto)}"));
        if (temAnterior)
            formulas.Add(new("Imposto desta parcela", $"{Formato.Moeda(apuracao.Imposto)} (ano) - {Formato.Moeda(r.ImpostoRetidoAnterior)} (já retido) = {Formato.Moeda(imposto)}{(apuracao.Imposto < r.ImpostoRetidoAnterior ? " (não fica negativo)" : "")}"));
        formulas.Add(new("Líquido", $"{Formato.Moeda(r.Valor)} - {Formato.Moeda(imposto)} (IRRF){(pensaoAlimenticia > 0m ? $" - {Formato.Moeda(pensaoAlimenticia)} (pensão)" : "")} = {Formato.Moeda(liquido)}"));

        var memoria = new List<GrupoMemoriaDto> { new("IRRF sobre a PLR", $"IRRF: {Formato.Moeda(imposto)}", formulas) };
        // O valor informado já aparece na base de cálculo; o percentual precisa da base da pensão explicada.
        if (r.Pensao is { EhPercentual: true } regraPercentual && calculoPensao is not null)
            memoria.Add(DemonstrativoPensao.Memoria("Pensão alimentícia sobre a PLR", regraPercentual, r.Valor, "PLR desta parcela", null, imposto, calculoPensao.Base, calculoPensao.Pensao, "Deduzida da base da tabela anual da PLR."));

        var faixas = tabelas.FaixasPlr.OrderBy(faixa => faixa.Numero).ToArray();
        var tabelaVigente = faixas.Select((faixa, indice) =>
        {
            var inicio = indice == 0 ? 0m : faixas[indice - 1].Limite + .01m;
            var intervalo = indice == faixas.Length - 1 ? $"Acima de {Formato.Moeda(faixas[indice - 1].Limite)}" : $"De {Formato.Moeda(inicio)} a {Formato.Moeda(faixa.Limite)}";
            return new FormulaDto(intervalo, faixa.Aliquota == 0m ? "Isento" : $"{Formato.Percentual(faixa.Aliquota)}, menos {Formato.Moeda(faixa.Deducao)}");
        }).ToArray();
        memoria.Add(new GrupoMemoriaDto("Tabela anual da PLR vigente", $"Competência {Formato.Competencia(tabelas.Competencia)}", tabelaVigente));

        return new DemonstrativoDto(
            "Participação nos lucros ou resultados (PLR)",
            $"Pagamento em {Formato.Competencia(tabelas.Competencia)}",
            [
                new("Líquido da PLR", Formato.Moeda(liquido), "Sem INSS e sem FGTS"),
                new("IRRF desta parcela", Formato.Moeda(imposto), imposto > 0m ? $"Faixa de {Formato.PercentualCurto(apuracao.Aliquota)} da tabela anual"
                    : apuracao.Aliquota == 0m ? "Dentro da faixa isenta" : "O imposto do ano já foi retido nas parcelas anteriores"),
                new("PLR no ano", Formato.Moeda(totalAno), temAnterior ? "Somando a PLR anterior" : "Primeiro pagamento do ano"),
                new("Isenção anual", Formato.Moeda(isencao), "Até esse total no ano, não há IRRF")
            ],
            [new("Participação nos lucros ou resultados", "", r.Valor)],
            descontos,
            informativos,
            memoria,
            [
                "A PLR tem tributação exclusiva na fonte, pela tabela anual, separada do salário e sem a dedução de dependentes, o desconto simplificado ou a redução mensal do IRRF.",
                "Com mais de um pagamento no ano, o imposto é recalculado sobre o total e o valor já retido é descontado (Lei 10.101/2000, art. 3º, § 7º). Se houve pensão na PLR anterior, informe a PLR anterior já descontada dela.",
                "Paga conforme a Lei 10.101/2000, no máximo duas vezes por ano e com intervalo de pelo menos um trimestre, a PLR não tem INSS nem FGTS."
            ]);
    }
}
