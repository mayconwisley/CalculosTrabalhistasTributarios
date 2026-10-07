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
/// Férias com o terço constitucional, o abono pecuniário (venda de até 1/3 dos dias) e o adiantamento opcional do 13º.
/// O abono e o seu terço não têm INSS nem IRRF; o IRRF das férias é calculado à parte dos demais rendimentos do mês.
/// </summary>
public sealed class SimularFeriasUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularFeriasRequest>
{
    private const int DiasMinimosDeGozo = 5;

    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularFeriasRequest request, CancellationToken cancellationToken)
    {
        if (request.Salario < 0m || request.Medias < 0m || request.Dependentes < 0 || request.Faltas < 0 || request.DiasGozo < 0 || request.PrevidenciaComplementar < 0m)
            return Erro.Validacao("Os valores, as faltas, os dias e a quantidade de dependentes não podem ser negativos.");
        var basesForaFerias = new[] { request.BaseSalarialForaFeriasNoMes, request.BaseForaFeriasMesSeguinte, request.BaseForaFeriasTerceiroMes };
        if (basesForaFerias.Any(valor => valor < 0m || valor > 1_000_000_000m || decimal.Round(valor, 2) != valor))
            return Erro.Validacao("As bases fora das férias devem ser valores não negativos, com centavos e de até R$ 1 bilhão por competência, sem incluir férias ou seu terço.");
        if (request.InicioGozo is null && (request.BaseForaFeriasMesSeguinte > 0m || request.BaseForaFeriasTerceiroMes > 0m))
            return Erro.Validacao("Informe o início do gozo para usar bases salariais do segundo ou terceiro mês.");

        var direito = RegrasTrabalhistas.DiasDeFeriasPorFaltas(request.Faltas);
        if (direito == 0)
            return Erro.Validacao("Com mais de 32 faltas injustificadas no período aquisitivo, não há direito a férias (CLT, art. 130).");

        var diasAbono = request.VenderAbono ? RegrasTrabalhistas.DiasDeAbonoPecuniario(direito) : 0;
        var diasDisponiveis = direito - diasAbono;
        var diasGozo = request.DiasGozo == 0 ? diasDisponiveis : request.DiasGozo;
        if (diasGozo > diasDisponiveis)
            return Erro.Validacao($"Os dias de descanso ({diasGozo}) passam dos {diasDisponiveis} dias disponíveis: {direito} de direito{(diasAbono > 0 ? $", menos {diasAbono} vendidos" : "")}.");
        if (diasGozo < DiasMinimosDeGozo)
            return Erro.Validacao("Cada período de férias deve ter pelo menos 5 dias de descanso (CLT, art. 134, § 1º).");

        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(request.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        var remuneracao = request.Salario + request.Medias;
        var ferias = CalculadoraTributacao.Arredondar(remuneracao / 30m * diasGozo);
        var terco = CalculadoraTributacao.Arredondar(ferias / 3m);
        var abono = CalculadoraTributacao.Arredondar(remuneracao / 30m * diasAbono);
        var tercoAbono = CalculadoraTributacao.Arredondar(abono / 3m);
        var adiantamento13 = request.AdiantarDecimoTerceiro ? CalculadoraTributacao.Arredondar(remuneracao / 2m) : 0m;

        var tributavel = ferias + terco;
        if (request.Pensao is { EhPercentual: false } informada && informada.Valor > tributavel)
            return Erro.Validacao($"A pensão informada ({Formato.Moeda(informada.Valor)}) é maior que as férias + 1/3 ({Formato.Moeda(tributavel)}).");
        var previdencia = request.PrevidenciaComplementar;
        if (previdencia > tributavel)
            return Erro.Validacao($"A previdência complementar informada ({Formato.Moeda(previdencia)}) é maior que as férias + 1/3 ({Formato.Moeda(tributavel)}).");

        // As férias entram na declaração anual: a previdência é deduzida por inteiro, como no salário do mês.
        var inss = tabelas.CalcularInss(tributavel);
        var valorInssFerias = inss.Valor;
        var conciliacoes = new List<ConcilicaoFeriasMes>();
        if (request.InicioGozo is { } inicioGozo)
        {
            var rateio = RateioFeriasPorCompetencia.Calcular(inicioGozo, diasGozo, ferias, terco);
            if (rateio.Falhou) return rateio.Erro;
            var parcelas = rateio.Valor;
            if (basesForaFerias.Skip(parcelas.Count).Any(valor => valor > 0m))
                return Erro.Validacao($"O gozo ocupa {parcelas.Count} competência(s). Zere as bases fora das férias dos meses que não fazem parte dele.");
            foreach (var (parcela, indice) in parcelas.Select((parcela, indice) => (parcela, indice)))
            {
                var consultaMes = await tributacaoConsulta.ObterTabelasAsync(parcela.Competencia, cancellationToken);
                if (consultaMes.Falhou) return consultaMes.Erro;
                var tabelaMes = consultaMes.Valor;
                var baseExterna = basesForaFerias[indice];
                var sobreFerias = tabelaMes.CalcularInss(parcela.BaseInss);
                var total = tabelaMes.CalcularInss(parcela.BaseInss + baseExterna);
                conciliacoes.Add(new(parcela, baseExterna, sobreFerias, total));
            }
            valorInssFerias = conciliacoes.Sum(item => item.SobreFerias.Valor);
        }
        var baseInssMes = tributavel + request.BaseSalarialForaFeriasNoMes;
        var inssMes = request.InicioGozo is null && request.BaseSalarialForaFeriasNoMes > 0m ? tabelas.CalcularInss(baseInssMes) : null;
        var inssResidualFolha = inssMes is null ? 0m : inssMes.Valor - inss.Valor;
        var calculoPensao = request.Pensao is { } regra
            ? CalculadoraPensao.Calcular(regra, tributavel, valorInssFerias, valor => tabelas.CalcularIrrf(tributavel, valorInssFerias, request.Dependentes, valor, previdenciaComplementar: previdencia), apuracao => apuracao.Imposto)
            : null;
        if (calculoPensao is { Falhou: true })
            return calculoPensao.Erro;
        var pensao = calculoPensao?.Valor;
        var irrf = pensao?.Apuracao ?? tabelas.CalcularIrrf(tributavel, valorInssFerias, request.Dependentes, previdenciaComplementar: previdencia);
        var valorPensao = pensao?.Pensao ?? 0m;
        var fgts = CalculadoraTributacao.Arredondar((tributavel + adiantamento13) * .08m);

        var proventos = new List<VerbaDto>
        {
            new("Férias", Formato.Dias(diasGozo), ferias),
            new("1/3 constitucional sobre as férias", "", terco)
        };
        if (diasAbono > 0)
        {
            proventos.Add(new("Abono pecuniário", Formato.Dias(diasAbono), abono));
            proventos.Add(new("1/3 sobre o abono pecuniário", "", tercoAbono));
        }
        if (adiantamento13 > 0m)
            proventos.Add(new("Adiantamento do 13º (1ª parcela)", "50%", adiantamento13));

        var descontos = new List<VerbaDto>
        {
            new("INSS sobre as férias", "", valorInssFerias),
            new(MemoriaTributaria.DescricaoIrrf("IRRF sobre as férias", irrf), MemoriaTributaria.ReferenciaIrrf(irrf), irrf.Imposto)
        };
        if (request.Pensao is { } regraPensao && valorPensao > 0m)
            descontos.Add(new("Pensão alimentícia sobre as férias", DemonstrativoPensao.Referencia(regraPensao), valorPensao));
        if (previdencia > 0m)
            descontos.Add(new("Previdência complementar sobre as férias", "", previdencia));
        var liquido = proventos.Sum(verba => verba.Valor) - descontos.Sum(verba => verba.Valor);
        var nomesDescontos = new List<string> { "INSS", "IRRF" };
        if (valorPensao > 0m) nomesDescontos.Add("pensão");
        if (previdencia > 0m) nomesDescontos.Add("previdência");

        var formulaRemuneracao = request.Medias > 0m
            ? $"({Formato.Moeda(request.Salario)} (salário) + {Formato.Moeda(request.Medias)} (médias))"
            : $"{Formato.Moeda(request.Salario)} (salário)";
        var restante = diasGozo < diasDisponiveis ? $"; restam {Formato.Dias(diasDisponiveis - diasGozo)} para outro período" : "";
        var formulas = new List<FormulaDto>
        {
            new("Dias de direito", $"{request.Faltas} falta(s) injustificada(s) no período aquisitivo: {Formato.Dias(direito)} de férias (CLT, art. 130)"),
            new("Divisão dos dias", diasAbono > 0
                ? $"{Formato.Dias(direito)} = {Formato.Dias(diasGozo)} de descanso + {Formato.Dias(diasAbono)} vendidos{restante}"
                : $"{Formato.Dias(diasGozo)} de descanso{restante}"),
            new("Férias", $"{formulaRemuneracao} ÷ 30 x {Formato.Dias(diasGozo)} = {Formato.Moeda(ferias)}"),
            new("1/3 constitucional", $"{Formato.Moeda(ferias)} ÷ 3 = {Formato.Moeda(terco)}")
        };
        if (diasAbono > 0)
        {
            formulas.Add(new("Abono pecuniário", $"{formulaRemuneracao} ÷ 30 x {Formato.Dias(diasAbono)} = {Formato.Moeda(abono)}"));
            formulas.Add(new("1/3 sobre o abono", $"{Formato.Moeda(abono)} ÷ 3 = {Formato.Moeda(tercoAbono)}"));
        }
        if (adiantamento13 > 0m)
            formulas.Add(new("Adiantamento do 13º", $"{formulaRemuneracao} x 50% = {Formato.Moeda(adiantamento13)}"));
        formulas.Add(new("Líquido", $"{Formato.Moeda(proventos.Sum(verba => verba.Valor))} (proventos) - {Formato.Moeda(valorInssFerias)} (INSS) - {Formato.Moeda(irrf.Imposto)} (IRRF){(valorPensao > 0m ? $" - {Formato.Moeda(valorPensao)} (pensão)" : "")}{(previdencia > 0m ? $" - {Formato.Moeda(previdencia)} (previdência complementar)" : "")} = {Formato.Moeda(liquido)}"));

        var observacoes = new List<string>
        {
            "O abono pecuniário e o seu terço não têm INSS, IRRF nem FGTS.",
            inssMes is null && conciliacoes.Count == 0
                ? "O IRRF das férias é calculado à parte dos demais rendimentos do mês. O recibo provisiona o INSS sobre as férias; na folha, confira o INSS da base reunida do mês, respeitando o teto."
                : "O IRRF das férias é calculado à parte dos demais rendimentos do mês. O recibo provisiona o INSS sobre as férias; a conciliação abaixo apura o INSS da base reunida do mês, respeitando o teto.",
            "As férias devem ser pagas até 2 dias antes do início do descanso (CLT, art. 145)."
        };
        if (inssMes is not null)
            observacoes.Add("A conciliação do INSS pressupõe pagamento e gozo na mesma competência e considera as férias + 1/3 e a base salarial fora das férias desse mês. O recibo mantém o INSS provisionado sobre as férias; na folha, confira o total sobre a base reunida e desconte o valor já provisionado. O IRRF das férias continua apurado separadamente. Pagamento ou gozo em competências distintas exige apuração de cada mês.");
        if (conciliacoes.Count > 0)
        {
            observacoes.Add("As férias e o terço foram distribuídos pelos dias gozados em cada competência. O INSS de cada folha usa a tabela desse mês e a respectiva base fora das férias; o IRRF do recibo usa a competência do pagamento. O saldo de INSS da folha desconta apenas a provisão das férias daquele mês. Ajuste as bases se houver outras verbas ou vínculos.");
            observacoes.Add("O FGTS exibido é o total estimado sobre as férias e o eventual adiantamento do 13º; confira sua distribuição e o recolhimento nas competências correspondentes.");
        }
        if (adiantamento13 > 0m)
            observacoes.Add("O adiantamento do 13º é pago sem descontos; o INSS e o IRRF são descontados na 2ª parcela, em dezembro.");
        if (pensao is not null)
            observacoes.Add("A pensão foi calculada sobre as férias + 1/3. O abono pecuniário, de natureza indenizatória, e o adiantamento do 13º ficaram fora da base; a pensão sobre o 13º é descontada na 2ª parcela.");
        if (previdencia > 0m)
            observacoes.Add(MemoriaTributaria.ObservacaoPrevidenciaMensal);

        var memoria = new List<GrupoMemoriaDto>
        {
            new("Férias", $"Líquido: {Formato.Moeda(liquido)}", formulas)
        };
        if (conciliacoes.Count == 0)
            memoria.Add(MemoriaTributaria.Inss("INSS sobre as férias", inss, "férias + 1/3"));
        else
            memoria.AddRange(conciliacoes.Select(item => MemoriaTributaria.Inss($"INSS das férias {Formato.Competencia(item.Parcela.Competencia)}", item.SobreFerias, "férias + 1/3 do mês")));
        memoria.Add(MemoriaTributaria.Irrf("IRRF sobre as férias", irrf, "férias + 1/3"));
        if (inssMes is not null)
            memoria.Add(new("Conciliação do INSS na folha do mês", $"INSS total: {Formato.Moeda(inssMes.Valor)}",
            [
                new("Base mensal", $"{Formato.Moeda(tributavel)} (férias + 1/3) + {Formato.Moeda(request.BaseSalarialForaFeriasNoMes)} (demais verbas) = {Formato.Moeda(baseInssMes)}"),
                new("INSS total", $"Tabela da competência sobre {Formato.Moeda(baseInssMes)} = {Formato.Moeda(inssMes.Valor)}"),
                new("Saldo a descontar na folha", $"{Formato.Moeda(inssMes.Valor)} - {Formato.Moeda(inss.Valor)} (provisionado nas férias) = {Formato.Moeda(inssResidualFolha)}")
            ]));
        foreach (var item in conciliacoes)
        {
            var competencia = Formato.Competencia(item.Parcela.Competencia);
            memoria.Add(new($"Folha de {competencia}", $"INSS total: {Formato.Moeda(item.Total.Valor)}; saldo na folha: {Formato.Moeda(item.SaldoFolha)}",
            [
                new("Dias de gozo", $"{Formato.Dias(item.Parcela.Dias)} nesta competência"),
                new("Férias + 1/3", $"{Formato.Moeda(item.Parcela.Ferias)} + {Formato.Moeda(item.Parcela.Terco)} = {Formato.Moeda(item.Parcela.BaseInss)}"),
                new("Base do INSS", $"{Formato.Moeda(item.Parcela.BaseInss)} (férias) + {Formato.Moeda(item.BaseForaFerias)} (demais verbas) = {Formato.Moeda(item.Parcela.BaseInss + item.BaseForaFerias)}"),
                new("INSS total da folha", $"Tabela de {competencia}: {Formato.Moeda(item.Total.Valor)}"),
                new("Saldo após provisão", $"{Formato.Moeda(item.Total.Valor)} - {Formato.Moeda(item.SobreFerias.Valor)} (INSS das férias deste mês) = {Formato.Moeda(item.SaldoFolha)}")
            ]));
        }
        if (request.Pensao is { } regraMemoria && pensao is not null)
            memoria.Add(DemonstrativoPensao.Memoria("Pensão alimentícia sobre as férias", regraMemoria, tributavel, "férias + 1/3", valorInssFerias, irrf.Imposto, pensao.Base, pensao.Pensao, "Deduzida da base do IRRF na modalidade de deduções legais; o desconto simplificado substitui essa dedução."));

        return new DemonstrativoDto(
            "Férias",
            $"Pagamento {Formato.Competencia(tabelas.Competencia)} • {Formato.Dias(diasGozo)} de descanso",
            [
                new("Líquido das férias", Formato.Moeda(liquido), $"Proventos menos {Formato.Lista(nomesDescontos)}"),
                new("Férias + 1/3", Formato.Moeda(tributavel), $"{Formato.Dias(diasGozo)} de descanso"),
                new("Abono + 1/3", Formato.Moeda(abono + tercoAbono), diasAbono > 0 ? $"{Formato.Dias(diasAbono)} vendidos, sem impostos" : "Sem venda de dias"),
                new("FGTS (8%)", Formato.Moeda(fgts), "Depositado pelo empregador"),
                .. (inssMes is null ? Array.Empty<DestaqueDto>() : new[] { new DestaqueDto("INSS da folha do mês", Formato.Moeda(inssMes.Valor), $"Saldo após provisão: {Formato.Moeda(inssResidualFolha)}") }),
                .. conciliacoes.Select(item => new DestaqueDto($"INSS folha {Formato.Competencia(item.Parcela.Competencia)}", Formato.Moeda(item.Total.Valor), $"Saldo após provisão: {Formato.Moeda(item.SaldoFolha)}"))
            ],
            proventos,
            descontos,
            [new("FGTS sobre férias + 1/3" + (adiantamento13 > 0m ? " e adiantamento do 13º" : ""), "8%", fgts)],
            memoria,
            observacoes);
    }

    private sealed record ConcilicaoFeriasMes(ParcelaFeriasCompetencia Parcela, decimal BaseForaFerias,
        ApuracaoInss SobreFerias, ApuracaoInss Total)
    {
        public decimal SaldoFolha => Total.Valor - SobreFerias.Valor;
    }
}
