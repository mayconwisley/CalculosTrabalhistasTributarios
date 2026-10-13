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
/// Confere o fracionamento em até três períodos e paga em dobro os dias gozados depois do período concessivo.
/// </summary>
public sealed class SimularFeriasUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularFeriasRequest>
{
    private const int DiasMinimosDeGozo = 5;

    // Com o fracionamento, um dos períodos tem pelo menos 14 dias corridos (CLT, art. 134, § 1º, desde a Lei 13.467/2017).
    private const int DiasDoPeriodoPrincipal = 14;
    private const int MaximoDePeriodos = 3;

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
        var outrosPeriodos = (request.OutrosPeriodos ?? []).Where(dias => dias != 0).ToArray();
        if (Fracionamento(diasGozo, outrosPeriodos, diasDisponiveis) is { } erroFracionamento)
            return Erro.Validacao(erroFracionamento);
        if (request.FimPeriodoConcessivo is not null && request.InicioGozo is null)
            return Erro.Validacao("Informe o início do gozo para conferir os dias depois do período concessivo.");
        var diasEmDobro = request.FimPeriodoConcessivo is { } fimConcessivo && request.InicioGozo is { } inicioDescanso
            ? Math.Clamp(inicioDescanso.AddDays(diasGozo - 1).DayNumber - fimConcessivo.DayNumber, 0, diasGozo)
            : 0;

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
        // A dobra repete a remuneração dos dias gozados fora do prazo, com o terço (CLT, art. 137; Súmula 81 do TST).
        var dobra = CalculadoraTributacao.Arredondar(remuneracao / 30m * diasEmDobro);
        var tercoDobra = CalculadoraTributacao.Arredondar(dobra / 3m);

        var tributavel = ferias + terco;
        // A dobra não integra o salário de contribuição (Lei 8.212/1991, art. 28, § 9º, "d") nem o FGTS (Lei 8.036/1990,
        // art. 15, § 6º), mas soma ao valor das férias pago, base do IRRF (RIR/2018, art. 682, § 1º).
        var baseIrrf = tributavel + dobra + tercoDobra;
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
            ? CalculadoraPensao.Calcular(regra, tributavel, valorInssFerias, valor => tabelas.CalcularIrrf(baseIrrf, valorInssFerias, request.Dependentes, valor, previdenciaComplementar: previdencia), apuracao => apuracao.Imposto)
            : null;
        if (calculoPensao is { Falhou: true })
            return calculoPensao.Erro;
        var pensao = calculoPensao?.Valor;
        var irrf = pensao?.Apuracao ?? tabelas.CalcularIrrf(baseIrrf, valorInssFerias, request.Dependentes, previdenciaComplementar: previdencia);
        var valorPensao = pensao?.Pensao ?? 0m;
        var fgts = CalculadoraTributacao.Arredondar((tributavel + adiantamento13) * .08m);

        var proventos = new List<VerbaDto>
        {
            new("Férias", Formato.Dias(diasGozo), ferias),
            new("1/3 constitucional sobre as férias", "", terco)
        };
        if (diasEmDobro > 0)
        {
            proventos.Add(new("Dobra das férias fora do período concessivo", Formato.Dias(diasEmDobro), dobra));
            proventos.Add(new("1/3 sobre a dobra das férias", "", tercoDobra));
        }
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
        var periodos = outrosPeriodos.Prepend(diasGozo).ToArray();
        var restanteDias = diasDisponiveis - periodos.Sum();
        var restante = outrosPeriodos.Length > 0
            ? $"; períodos de {string.Join(" + ", periodos.Select(dias => dias.ToString(Formato.Cultura)))} = {Formato.Dias(periodos.Sum())}{(restanteDias > 0 ? $", restam {Formato.Dias(restanteDias)} para outro período" : "")}"
            : diasGozo < diasDisponiveis ? $"; restam {Formato.Dias(diasDisponiveis - diasGozo)} para outro período" : "";
        var formulas = new List<FormulaDto>
        {
            new("Dias de direito", $"{request.Faltas} falta(s) injustificada(s) no período aquisitivo: {Formato.Dias(direito)} de férias (CLT, art. 130)"),
            new("Divisão dos dias", diasAbono > 0
                ? $"{Formato.Dias(direito)} = {Formato.Dias(diasGozo)} de descanso + {Formato.Dias(diasAbono)} vendidos{restante}"
                : $"{Formato.Dias(diasGozo)} de descanso{restante}"),
            new("Férias", $"{formulaRemuneracao} ÷ 30 x {Formato.Dias(diasGozo)} = {Formato.Moeda(ferias)}"),
            new("1/3 constitucional", $"{Formato.Moeda(ferias)} ÷ 3 = {Formato.Moeda(terco)}")
        };
        if (diasEmDobro > 0)
        {
            formulas.Add(new("Dias fora do período concessivo", $"Gozo de {Formato.Data(request.InicioGozo!.Value)} a {Formato.Data(request.InicioGozo.Value.AddDays(diasGozo - 1))}; período concessivo até {Formato.Data(request.FimPeriodoConcessivo!.Value)}: {Formato.Dias(diasEmDobro)} em dobro (CLT, art. 137)"));
            formulas.Add(new("Dobra das férias", $"{formulaRemuneracao} ÷ 30 x {Formato.Dias(diasEmDobro)} = {Formato.Moeda(dobra)}"));
            formulas.Add(new("1/3 sobre a dobra", $"{Formato.Moeda(dobra)} ÷ 3 = {Formato.Moeda(tercoDobra)}"));
        }
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
        if (diasEmDobro > 0)
            observacoes.Add("A dobra das férias gozadas depois do período concessivo, com o seu terço, não tem INSS nem FGTS (Lei 8.212/1991, art. 28, § 9º, \"d\"; Lei 8.036/1990, art. 15, § 6º), mas entra no IRRF das férias com as demais parcelas. O pagamento atrasado de férias gozadas no prazo não gera dobra: o STF declarou inconstitucional a Súmula 450 do TST (ADPF 501).");
        else if (request.FimPeriodoConcessivo is not null)
            observacoes.Add("O gozo termina dentro do período concessivo: não há dobra (CLT, art. 137).");
        if (request.InicioGozo is { DayOfWeek: DayOfWeek.Friday or DayOfWeek.Saturday })
            observacoes.Add("O início das férias não pode cair nos dois dias que antecedem feriado ou o repouso semanal (CLT, art. 134, § 3º). Com o repouso no domingo, sexta e sábado não podem ser o primeiro dia; confira também os feriados.");
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

    /// <summary>
    /// Confere a divisão em até três períodos: cada um com pelo menos 5 dias e um deles com pelo menos 14, sem passar dos
    /// dias disponíveis. Sem os demais períodos informados, confere se o saldo ainda forma um período válido.
    /// </summary>
    private static string? Fracionamento(int diasGozo, int[] outros, int diasDisponiveis)
    {
        var periodos = outros.Prepend(diasGozo).ToArray();
        if (periodos.Length > MaximoDePeriodos)
            return "As férias podem ser divididas em até três períodos (CLT, art. 134, § 1º).";
        if (outros.Any(dias => dias < DiasMinimosDeGozo))
            return "Cada período de férias deve ter pelo menos 5 dias de descanso (CLT, art. 134, § 1º).";
        var saldo = diasDisponiveis - periodos.Sum();
        if (saldo < 0)
            return $"Os períodos somam {periodos.Sum()} dias e passam dos {diasDisponiveis} dias disponíveis.";
        var temPrincipal = periodos.Any(dias => dias >= DiasDoPeriodoPrincipal);
        if (saldo == 0)
            return temPrincipal ? null : "Um dos períodos de férias deve ter pelo menos 14 dias corridos (CLT, art. 134, § 1º).";
        // O saldo vira mais um período: precisa de vaga, de 5 dias e, sem um período de 14 dias, de ser ele esse período.
        var minimoDoSaldo = temPrincipal ? DiasMinimosDeGozo : DiasDoPeriodoPrincipal;
        if (periodos.Length == MaximoDePeriodos || saldo < minimoDoSaldo)
            return $"Sobram {saldo} dia(s) sem formar um período válido: as férias podem ter até três períodos, cada um com pelo menos 5 dias e um deles com pelo menos 14 (CLT, art. 134, § 1º). Ajuste os dias de descanso{(outros.Length > 0 ? " ou dos demais períodos" : "")}.";
        return null;
    }

    private sealed record ConcilicaoFeriasMes(ParcelaFeriasCompetencia Parcela, decimal BaseForaFerias,
        ApuracaoInss SobreFerias, ApuracaoInss Total)
    {
        public decimal SaldoFolha => Total.Valor - SobreFerias.Valor;
    }
}
