using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// 13º salário (Leis 4.090/1962 e 4.749/1965): a 1ª parcela, paga até 30/11, não tem descontos; a 2ª, paga até 20/12,
/// desconta a 1ª, o INSS e o IRRF, ambos calculados sobre o valor integral e à parte do salário do mês.
/// </summary>
public sealed class SimularDecimoTerceiroUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularDecimoTerceiroRequest>
{
    // A previdência complementar deduzida do IRRF fica limitada a 12% dos rendimentos da base (Lei 9.532/1997, art. 11).
    // O 13º é tributado à parte e não entra na declaração anual: o limite é aplicado na fonte, sobre o próprio 13º
    // (Solução de Consulta Cosit 185/2024).
    private const decimal LimitePrevidencia = 12m;

    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularDecimoTerceiroRequest request, CancellationToken cancellationToken)
    {
        if (request.Salario < 0m || request.Medias < 0m || request.Dependentes < 0 || request.ValorAdiantamento < 0m || request.PrevidenciaComplementar < 0m)
            return Erro.Validacao("Os valores e a quantidade de dependentes não podem ser negativos.");
        if (request.Avos is < 1 or > 12)
            return Erro.Validacao("Os avos devem estar entre 1 e 12: um para cada mês com 15 dias ou mais de trabalho no ano.");

        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(request.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        var remuneracao = request.Salario + request.Medias;
        var integral = CalculadoraTributacao.Arredondar(remuneracao / 12m * request.Avos);
        var adiantamento = request.Adiantamento switch
        {
            AdiantamentoDecimoTerceiro.CinquentaPorCento => CalculadoraTributacao.Arredondar(integral / 2m),
            AdiantamentoDecimoTerceiro.SemAdiantamento => 0m,
            _ => request.ValorAdiantamento
        };
        if (adiantamento > integral)
            return Erro.Validacao($"O adiantamento informado ({Formato.Moeda(adiantamento)}) é maior que o 13º integral ({Formato.Moeda(integral)}).");

        if (request.Pensao is { EhPercentual: false } informada && informada.Valor > integral)
            return Erro.Validacao($"A pensão informada ({Formato.Moeda(informada.Valor)}) é maior que o 13º integral ({Formato.Moeda(integral)}).");

        if (request.PrevidenciaComplementar > integral)
            return Erro.Validacao($"A previdência complementar informada ({Formato.Moeda(request.PrevidenciaComplementar)}) é maior que o 13º integral ({Formato.Moeda(integral)}).");
        var previdencia = request.PrevidenciaComplementar;
        var limitePrevidencia = CalculadoraTributacao.Arredondar(integral * LimitePrevidencia / 100m);
        var previdenciaDedutivel = Math.Min(previdencia, limitePrevidencia);

        var inss = tabelas.CalcularInss(integral);
        var calculoPensao = request.Pensao is { } regra
            ? CalculadoraPensao.Calcular(regra, integral, inss.Valor, valor => tabelas.CalcularIrrf(integral, inss.Valor, request.Dependentes, valor, tributacaoExclusiva: true, previdenciaComplementar: previdenciaDedutivel), apuracao => apuracao.Imposto)
            : null;
        if (calculoPensao is { Falhou: true })
            return calculoPensao.Erro;
        var pensao = calculoPensao?.Valor;
        var irrf = pensao?.Apuracao ?? tabelas.CalcularIrrf(integral, inss.Valor, request.Dependentes, tributacaoExclusiva: true, previdenciaComplementar: previdenciaDedutivel);
        var valorPensao = pensao?.Pensao ?? 0m;
        var segundaParcela = integral - adiantamento - inss.Valor - irrf.Imposto - valorPensao - previdencia;
        var liquidoTotal = integral - inss.Valor - irrf.Imposto - valorPensao - previdencia;
        var fgts = CalculadoraTributacao.Arredondar(integral * .08m);

        var formulaIntegral = request.Medias > 0m
            ? $"({Formato.Moeda(request.Salario)} (salário) + {Formato.Moeda(request.Medias)} (médias)) ÷ 12 x {request.Avos} avos = {Formato.Moeda(integral)}"
            : $"{Formato.Moeda(request.Salario)} (salário) ÷ 12 x {request.Avos} avos = {Formato.Moeda(integral)}";
        var formulaAdiantamento = request.Adiantamento switch
        {
            AdiantamentoDecimoTerceiro.CinquentaPorCento => $"{Formato.Moeda(integral)} x 50% = {Formato.Moeda(adiantamento)}",
            AdiantamentoDecimoTerceiro.SemAdiantamento => "Não houve adiantamento: o 13º é pago de uma vez, em dezembro.",
            _ => $"Valor informado: {Formato.Moeda(adiantamento)}"
        };
        var descricaoAdiantamento = request.Adiantamento == AdiantamentoDecimoTerceiro.SemAdiantamento ? "Pagamento único" : "Até 30/11, sem descontos";

        var descontos = new List<VerbaDto>();
        if (adiantamento > 0m)
            descontos.Add(new("Adiantamento do 13º (1ª parcela)", request.Adiantamento == AdiantamentoDecimoTerceiro.CinquentaPorCento ? "50%" : "", adiantamento));
        descontos.Add(new("INSS sobre o 13º", "", inss.Valor));
        descontos.Add(new(MemoriaTributaria.DescricaoIrrf("IRRF sobre o 13º", irrf), MemoriaTributaria.ReferenciaIrrf(irrf), irrf.Imposto));
        if (request.Pensao is { } regraPensao && valorPensao > 0m)
            descontos.Add(new("Pensão alimentícia sobre o 13º", DemonstrativoPensao.Referencia(regraPensao), valorPensao));
        if (previdencia > 0m)
            descontos.Add(new("Previdência complementar sobre o 13º", "", previdencia));

        var memoria = new List<GrupoMemoriaDto>
        {
            new("13º salário", $"2ª parcela: {Formato.Moeda(segundaParcela)}",
            [
                new("Valor integral", formulaIntegral),
                new("1ª parcela", formulaAdiantamento),
                new("2ª parcela", $"{Formato.Moeda(integral)} (integral) - {Formato.Moeda(adiantamento)} (1ª parcela) - {Formato.Moeda(inss.Valor)} (INSS) - {Formato.Moeda(irrf.Imposto)} (IRRF){(valorPensao > 0m ? $" - {Formato.Moeda(valorPensao)} (pensão)" : "")}{(previdencia > 0m ? $" - {Formato.Moeda(previdencia)} (previdência complementar)" : "")} = {Formato.Moeda(segundaParcela)}")
            ]),
            MemoriaTributaria.Inss("INSS sobre o 13º", inss, "13º integral"),
            MemoriaTributaria.Irrf("IRRF sobre o 13º", irrf, "13º integral")
        };
        if (previdencia > 0m)
            memoria.Add(new("Previdência complementar sobre o 13º", $"Dedução no IRRF: {Formato.Moeda(previdenciaDedutivel)}",
            [
                new("Limite de 12%", $"{Formato.Moeda(integral)} (13º integral) x 12% = {Formato.Moeda(limitePrevidencia)}"),
                new("Dedução no IRRF", previdencia > limitePrevidencia
                    ? $"A contribuição de {Formato.Moeda(previdencia)} passa do limite: são deduzidos {Formato.Moeda(limitePrevidencia)}, e os {Formato.Moeda(previdencia - limitePrevidencia)} restantes não reduzem o IRRF do 13º"
                    : $"A contribuição de {Formato.Moeda(previdencia)} cabe no limite e é deduzida por inteiro, na modalidade de deduções legais")
            ]));
        var observacoes = new List<string>
        {
            "Cada mês do ano com 15 dias ou mais de trabalho dá direito a 1/12 do 13º.",
            "O INSS e o IRRF do 13º são calculados sobre o valor integral, à parte do salário de dezembro; o IRRF do 13º é de tributação exclusiva na fonte.",
            "O FGTS é depositado sobre cada parcela no mês em que ela é paga."
        };
        if (request.Pensao is { } regraMemoria && pensao is not null)
        {
            memoria.Add(DemonstrativoPensao.Memoria("Pensão alimentícia sobre o 13º", regraMemoria, integral, "13º integral", inss.Valor, irrf.Imposto, pensao.Base, pensao.Pensao, "Deduzida da base do IRRF na modalidade de deduções legais; o desconto simplificado substitui essa dedução."));
            observacoes.Add("A pensão foi calculada sobre o 13º integral e descontada na 2ª parcela. Se a empresa já descontou pensão na 1ª parcela, abata esse valor.");
        }
        if (previdencia > 0m)
            observacoes.Add("A previdência complementar sobre o 13º é deduzida da base do IRRF do 13º até 12% do valor integral (Lei 9.532/1997, art. 11; Solução de Consulta Cosit 185/2024), só na modalidade de deduções legais: o desconto simplificado substitui essa dedução. Como o 13º é tributado à parte, o limite é aplicado na fonte. A contribuição não reduz o INSS, o FGTS nem a base da pensão.");

        // Descontos da 2ª parcela, para o resumo e o aviso de parcela negativa.
        var descontosParcela = new List<string> { "INSS", "IRRF" };
        if (valorPensao > 0m) descontosParcela.Add("pensão");
        if (previdencia > 0m) descontosParcela.Add("previdência");
        if (segundaParcela < 0m)
            observacoes.Insert(0, $"A 2ª parcela ficou negativa em {Formato.Moeda(-segundaParcela)}: a 1ª parcela já paga supera o que resta do 13º depois de {Formato.Lista(descontosParcela)}. A diferença precisa ser descontada de outra verba{(valorPensao > 0m ? ", ou parte da pensão deve ser descontada já na 1ª parcela" : "")}.");

        return new DemonstrativoDto(
            "13º salário",
            $"Competência {Formato.Competencia(tabelas.Competencia)} • {Formato.Avos(request.Avos)}",
            [
                new("1ª parcela", Formato.Moeda(adiantamento), descricaoAdiantamento),
                new("2ª parcela líquida", Formato.Moeda(segundaParcela), $"Até 20/12, com {Formato.Lista(descontosParcela)}"),
                new("13º líquido total", Formato.Moeda(liquidoTotal), $"Integral de {Formato.Moeda(integral)}"),
                new("FGTS (8%)", Formato.Moeda(fgts), "Depositado pelo empregador")
            ],
            [new("13º salário integral", Formato.Avos(request.Avos), integral)],
            descontos,
            [new("FGTS sobre o 13º", "8%", fgts)],
            memoria,
            observacoes);
    }
}
