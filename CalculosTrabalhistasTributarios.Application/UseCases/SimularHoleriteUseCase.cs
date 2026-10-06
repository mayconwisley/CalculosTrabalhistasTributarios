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
/// Holerite do mês: salário, insalubridade ou periculosidade, horas extras, adicional noturno e DSR, faltas, atrasos,
/// INSS, IRRF, pensão alimentícia, vale-transporte e salário-família em um só demonstrativo. As horas seguem as mesmas
/// regras da calculadora de horas extras, e os adicionais, as da calculadora de insalubridade e periculosidade.
/// Cada provento entra só nas bases em que incide: os tributáveis no INSS, no IRRF e no FGTS; os prêmios só no IRRF;
/// os não tributáveis, como ajuda de custo e reembolsos, em nenhuma.
/// </summary>
public sealed class SimularHoleriteUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularHoleriteRequest>
{
    private const decimal PercentualPericulosidade = 30m;
    private const decimal PercentualValeTransporte = 6m;

    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularHoleriteRequest r, CancellationToken cancellationToken)
    {
        if (r.Salario < 0m)
            return Erro.Validacao("O salário não pode ser negativo.");
        if (r.Salario == 0m && r.Comissoes == 0m)
            return Erro.Validacao("Informe o salário ou as comissões do mês.");
        if (r.OutrosProventos < 0m || r.Comissoes < 0m || r.PisoGarantidoComissoes < 0m || r.Premios < 0m || r.ProventosNaoTributaveis < 0m || r.HorasAtraso < 0m || r.Faltas < 0 || r.DescansosPerdidos < 0 || r.Dependentes < 0 || r.Filhos < 0
            || r.CustoValeTransporte < 0m || r.Adiantamento < 0m || r.OutrosDescontos < 0m || r.PrevidenciaComplementar < 0m)
            return Erro.Validacao("Os valores, as horas, as faltas e as quantidades não podem ser negativos.");
        // Os dois são descontados pelo salário-dia: juntos não passam dos 30 dias do mês comercial.
        if (r.Faltas + r.DescansosPerdidos > 30)
            return Erro.Validacao("As faltas e os descansos perdidos, somados, não podem passar de 30 dias no mês.");
        var informadas = new HorasInformadas(r.HorasFaixa1, r.PercentualFaixa1, r.HorasFaixa2, r.PercentualFaixa2, r.HorasNoturnas, r.PercentualNoturno, r.HorasExtrasNoturnas, r.Feriados, r.Rural);
        if (r.Salario == 0m && r.Comissoes > 0m && (r.HorasFaixa1 > 0m || r.HorasFaixa2 > 0m || r.HorasNoturnas > 0m || r.HorasExtrasNoturnas > 0m))
            return Erro.Validacao("As horas extras e noturnas do comissionista puro exigem cálculo sobre a remuneração variável e as horas efetivamente trabalhadas; este holerite não dispõe desses dados. Informe as verbas já apuradas em Proventos tributáveis, com o DSR correspondente.");
        if (CalculadoraHoras.Validar(informadas, r.Divisor) is { Falhou: true } horasInvalidas)
            return horasInvalidas.Erro;

        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(r.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;
        var observacoes = new List<string>();

        // Insalubridade sobre o salário mínimo e periculosidade de 30% do salário; os dois não se acumulam (CLT, art. 193, § 2º).
        var salarioMinimo = 0m;
        var insalubridade = 0m;
        if (r.Insalubridade != GrauInsalubridade.Nenhum)
        {
            var consultaSalarioMinimo = tabelas.ObterSalarioMinimo();
            if (consultaSalarioMinimo.Falhou)
                return consultaSalarioMinimo.Erro;
            salarioMinimo = consultaSalarioMinimo.Valor;
            insalubridade = CalculadoraTributacao.Arredondar(salarioMinimo * (int)r.Insalubridade / 100m);
        }
        var periculosidade = r.Periculosidade ? CalculadoraTributacao.Arredondar(r.Salario * PercentualPericulosidade / 100m) : 0m;
        var aplicaPericulosidade = r.Periculosidade && periculosidade >= insalubridade;
        var adicional = aplicaPericulosidade ? periculosidade : insalubridade;
        if (r.Periculosidade && r.Insalubridade != GrauInsalubridade.Nenhum)
            observacoes.Add($"Insalubridade e periculosidade não se acumulam: foi aplicado o adicional de {(aplicaPericulosidade ? "periculosidade" : "insalubridade")}, o mais vantajoso (CLT, art. 193, § 2º).");

        // O salário e o adicional formam o valor da hora e o salário-dia das faltas.
        var baseHora = r.Salario + adicional;
        var calculoHoras = CalculadoraHoras.Calcular(r.Competencia, baseHora, r.Divisor, informadas);
        if (calculoHoras.Falhou)
            return calculoHoras.Erro;
        var horas = calculoHoras.Valor;
        var repousosDoPeriodo = r.Comissoes > 0m && r.DiasDescansoComissoes is { } repousosInformados
            ? Math.Max(horas.DiasDescanso, repousosInformados) : horas.DiasDescanso;
        if (r.DescansosPerdidos > repousosDoPeriodo)
            return Erro.Validacao($"Os descansos perdidos ({r.DescansosPerdidos}) passam dos {repousosDoPeriodo} repousos do período.");
        ComissoesApuradas? comissoes = null;
        if (r.Comissoes > 0m)
        {
            var apuracao = CalculadoraComissoes.Calcular(new ComissoesInformadas(r.Competencia, r.Comissoes,
                r.ComissoesIncluemDsr, r.Feriados, r.DescansosPerdidos, r.DiasUteisComissoes, r.DiasDescansoComissoes));
            if (apuracao.Falhou)
                return apuracao.Erro;
            comissoes = apuracao.Valor;
        }
        var faltas = CalculadoraTributacao.Arredondar(baseHora * r.Faltas / 30m);
        var descansos = CalculadoraTributacao.Arredondar(baseHora * r.DescansosPerdidos / 30m);
        var atrasos = CalculadoraTributacao.Arredondar(baseHora * r.HorasAtraso / r.Divisor);

        var tributaveisSemComplemento = baseHora + r.OutrosProventos + (comissoes?.Total ?? 0m) + horas.Variaveis + horas.Dsr;
        var complementoComissoes = 0m;
        var pisoComissoes = 0m;
        if (comissoes is not null)
        {
            var garantia = GarantiaComissionista.ObterPiso(tabelas, r.PisoGarantidoComissoes, comissoes.DiasInformados);
            if (garantia.Falhou)
                return garantia.Erro;
            pisoComissoes = garantia.Valor;
            complementoComissoes = CalculadoraComissoes.ComplementoGarantiaMinima(tributaveisSemComplemento, pisoComissoes);
        }
        var tributaveis = tributaveisSemComplemento + complementoComissoes;
        var remuneracao = tributaveis - faltas - descansos - atrasos;
        if (remuneracao < 0m)
            return Erro.Validacao($"As faltas e os atrasos ({Formato.Moeda(faltas + descansos + atrasos)}) passam da remuneração do mês ({Formato.Moeda(tributaveis)}).");

        // Os prêmios não são salário de contribuição nem entram no FGTS (Lei 8.212/1991, art. 28, § 9º, z; Lei 8.036/1990,
        // art. 15, § 6º), mas são rendimento tributável: somam-se à remuneração só no IRRF e na base da pensão.
        var rendimentos = remuneracao + r.Premios;
        var rotuloRendimentos = r.Premios > 0m ? "remuneração e prêmios" : "remuneração do mês";
        var inss = tabelas.CalcularInss(remuneracao);
        if (r.Pensao is { EhPercentual: false } informada && informada.Valor > rendimentos)
            return Erro.Validacao($"A pensão informada ({Formato.Moeda(informada.Valor)}) é maior que os rendimentos tributáveis do mês ({Formato.Moeda(rendimentos)}).");
        // A previdência complementar reduz a base do IRRF, mas não a da pensão: é desconto voluntário, não obrigatório.
        var calculoPensao = r.Pensao is { } regra
            ? CalculadoraPensao.Calcular(regra, rendimentos, inss.Valor, valor => tabelas.CalcularIrrf(rendimentos, inss.Valor, r.Dependentes, valor, previdenciaComplementar: r.PrevidenciaComplementar), apuracao => apuracao.Imposto)
            : null;
        if (calculoPensao is { Falhou: true })
            return calculoPensao.Erro;
        var pensao = calculoPensao?.Valor;
        var irrf = pensao?.Apuracao ?? tabelas.CalcularIrrf(rendimentos, inss.Valor, r.Dependentes, previdenciaComplementar: r.PrevidenciaComplementar);
        var valorPensao = pensao?.Pensao ?? 0m;

        // Salário-família pela remuneração do mês, sem INSS, IRRF nem FGTS (Lei 8.213/1991, arts. 65 a 70).
        FaixaSalarioFamilia? faixaFamilia = null;
        var salarioFamilia = 0m;
        if (r.Filhos > 0)
        {
            if (tabelas.FaixasSalarioFamilia.Count == 0)
                return Erro.NaoEncontrado($"Não há tabela de salário-família cadastrada para a competência {Formato.Competencia(r.Competencia)}.");
            faixaFamilia = tabelas.FaixasSalarioFamilia.OrderBy(item => item.Faixa).FirstOrDefault(item => remuneracao <= item.LimiteRemuneracao);
            salarioFamilia = (faixaFamilia?.Cota ?? 0m) * r.Filhos;
        }

        // O empregado paga até 6% do salário-base, sem adicionais; a empresa paga o restante (Lei 7.418/1985, art. 4º).
        var seisPorCento = CalculadoraTributacao.Arredondar(r.Salario * PercentualValeTransporte / 100m);
        var valeTransporte = Math.Min(seisPorCento, r.CustoValeTransporte);
        var fgts = CalculadoraTributacao.Arredondar(remuneracao * .08m);

        var nomeAdicional = aplicaPericulosidade ? "Adicional de periculosidade" : $"Adicional de insalubridade ({NomeGrau(r.Insalubridade)})";
        var proventos = new List<VerbaDto>();
        if (r.Salario > 0m) proventos.Add(new("Salário", Formato.Dias(30), r.Salario));
        if (adicional > 0m) proventos.Add(new(nomeAdicional, Formato.PercentualCurto(aplicaPericulosidade ? PercentualPericulosidade : (int)r.Insalubridade), adicional));
        if (comissoes is not null)
        {
            proventos.Add(new("Comissões", "", comissoes.Comissoes));
            proventos.Add(new("DSR sobre comissões", $"{comissoes.DescansosPagos} descansos", comissoes.Dsr));
        }
        if (complementoComissoes > 0m) proventos.Add(new("Complemento da garantia mínima", "", complementoComissoes));
        if (r.OutrosProventos > 0m) proventos.Add(new("Outros proventos tributáveis", "", r.OutrosProventos));
        proventos.AddRange(DemonstrativoHoras.Proventos(horas));
        if (r.Premios > 0m) proventos.Add(new("Prêmios", "", r.Premios));
        if (r.ProventosNaoTributaveis > 0m) proventos.Add(new("Proventos não tributáveis (ajuda de custo, diárias, reembolsos)", "", r.ProventosNaoTributaveis));
        if (salarioFamilia > 0m) proventos.Add(new("Salário-família", r.Filhos == 1 ? "1 filho" : $"{r.Filhos} filhos", salarioFamilia));

        var descontos = new List<VerbaDto>();
        if (faltas > 0m) descontos.Add(new("Faltas injustificadas", Formato.Dias(r.Faltas), faltas));
        if (descansos > 0m) descontos.Add(new("DSR perdido pelas faltas", Formato.Dias(r.DescansosPerdidos), descansos));
        if (atrasos > 0m) descontos.Add(new("Atrasos e saídas antecipadas", Formato.Horas(r.HorasAtraso), atrasos));
        descontos.Add(new("INSS", "", inss.Valor));
        descontos.Add(new(MemoriaTributaria.DescricaoIrrf("IRRF", irrf), MemoriaTributaria.ReferenciaIrrf(irrf), irrf.Imposto));
        if (r.Pensao is { } regraPensao && valorPensao > 0m) descontos.Add(new("Pensão alimentícia", DemonstrativoPensao.Referencia(regraPensao), valorPensao));
        if (r.PrevidenciaComplementar > 0m) descontos.Add(new("Previdência complementar", "", r.PrevidenciaComplementar));
        if (valeTransporte > 0m) descontos.Add(new("Vale-transporte", valeTransporte == seisPorCento ? "6%" : "", valeTransporte));
        if (r.Adiantamento > 0m) descontos.Add(new("Adiantamento salarial", "", r.Adiantamento));
        if (r.OutrosDescontos > 0m) descontos.Add(new("Outros descontos", "", r.OutrosDescontos));

        var totalProventos = proventos.Sum(verba => verba.Valor);
        var totalDescontos = descontos.Sum(verba => verba.Valor);
        var liquido = totalProventos - totalDescontos;
        if (liquido < 0m)
            return Erro.Validacao($"Os descontos ({Formato.Moeda(totalDescontos)}) passam dos proventos ({Formato.Moeda(totalProventos)}). Revise as faltas, o adiantamento e os outros descontos.");

        var formulasRemuneracao = FormulasRemuneracao(r, horas, salarioMinimo, insalubridade, periculosidade, aplicaPericulosidade, faltas, descansos, atrasos, tributaveis, remuneracao);
        if (r.Premios > 0m)
            formulasRemuneracao.Add(new("Rendimentos do IRRF", $"{Formato.Moeda(remuneracao)} (remuneração) + {Formato.Moeda(r.Premios)} (prêmios, sem INSS e FGTS) = {Formato.Moeda(rendimentos)}"));
        var memoria = new List<GrupoMemoriaDto> { new("Remuneração do mês", $"Remuneração: {Formato.Moeda(remuneracao)}", formulasRemuneracao) };
        if (comissoes is not null)
        {
            var formulasComissoes = new List<FormulaDto>(DemonstrativoComissoes.Formulas(comissoes))
            {
                new("Garantia mínima", $"Máximo entre {Formato.Moeda(0m)} e {Formato.Moeda(pisoComissoes)} (piso) - {Formato.Moeda(tributaveisSemComplemento)} (remuneração antes do complemento) = {Formato.Moeda(complementoComissoes)}")
            };
            memoria.Add(new("Comissões e DSR", $"Total: {Formato.Moeda(comissoes.Total)}", formulasComissoes));
        }
        memoria.Add(MemoriaTributaria.Inss("INSS", inss, "remuneração do mês"));
        memoria.Add(MemoriaTributaria.Irrf("IRRF", irrf, rotuloRendimentos));
        if (r.Pensao is { } regraMemoria && pensao is not null)
            memoria.Add(DemonstrativoPensao.Memoria("Pensão alimentícia", regraMemoria, rendimentos, rotuloRendimentos, inss.Valor, irrf.Imposto, pensao.Base, pensao.Pensao, "Deduzida da base do IRRF na modalidade de deduções legais; o desconto simplificado substitui essa dedução."));
        if (r.Filhos > 0)
            memoria.Add(new("Salário-família", $"Valor: {Formato.Moeda(salarioFamilia)}",
            [
                faixaFamilia is null
                    ? new("Direito", $"{Formato.Moeda(remuneracao)} passa do limite de {Formato.Moeda(tabelas.FaixasSalarioFamilia.Max(item => item.LimiteRemuneracao))}: sem direito neste mês.")
                    : new("Direito", $"{Formato.Moeda(remuneracao)} não passa do limite de {Formato.Moeda(faixaFamilia.LimiteRemuneracao)}: cota de {Formato.Moeda(faixaFamilia.Cota)} por filho."),
                new("Valor", $"{Formato.Moeda(faixaFamilia?.Cota ?? 0m)} x {r.Filhos} = {Formato.Moeda(salarioFamilia)}")
            ]));
        if (r.CustoValeTransporte > 0m)
            memoria.Add(new("Vale-transporte", $"Desconto: {Formato.Moeda(valeTransporte)}",
            [
                new("Limite de 6%", $"{Formato.Moeda(r.Salario)} (salário-base) x 6% = {Formato.Moeda(seisPorCento)}"),
                new("Desconto", $"O menor entre {Formato.Moeda(seisPorCento)} e o custo de {Formato.Moeda(r.CustoValeTransporte)} = {Formato.Moeda(valeTransporte)}; a empresa paga {Formato.Moeda(r.CustoValeTransporte - valeTransporte)}")
            ]));
        memoria.Add(new("Líquido", $"Líquido: {Formato.Moeda(liquido)}",
            [new("Proventos menos descontos", $"{Formato.Moeda(totalProventos)} - {Formato.Moeda(totalDescontos)} = {Formato.Moeda(liquido)}")]));

        var informativos = new List<VerbaDto>
        {
            new("FGTS", "8%", fgts),
            new("Base do INSS e do FGTS", "", remuneracao),
            new(MemoriaTributaria.DescricaoIrrf("Base do IRRF", irrf), "", irrf.Aplicada.BaseCalculo)
        };
        if (salarioFamilia > 0m) informativos.Add(new("Salário-família deduzido pela empresa das contribuições ao INSS", "", salarioFamilia));
        if (r.CustoValeTransporte > valeTransporte) informativos.Add(new("Vale-transporte pago pela empresa", "", r.CustoValeTransporte - valeTransporte));

        if (r.Faltas > 0 || r.DescansosPerdidos > 0)
            observacoes.Add("As faltas e os descansos perdidos são descontados pelo salário-dia (salário e adicional ÷ 30). A falta injustificada faz perder o descanso remunerado da semana (Lei 605/1949, art. 6º): informe um descanso perdido por semana com falta, e também o feriado dessa semana.");
        if (comissoes is not null)
            observacoes.Add("As comissões e seu DSR entram no INSS, no IRRF e no FGTS. O DSR das comissões foi calculado separadamente do DSR das horas extras; não repita as comissões em Proventos tributáveis. A garantia mínima considera a remuneração antes dos descontos por faltas e atrasos.");
        if (comissoes is not null && (r.HorasFaixa1 > 0m || r.HorasFaixa2 > 0m || r.HorasExtrasNoturnas > 0m))
            observacoes.Add("Com remuneração mista, as horas extras calculadas aqui usam somente a parcela fixa e os adicionais salariais informados. O adicional de horas extras sobre as comissões segue regra própria (Súmula 340 do TST) e depende das horas efetivamente trabalhadas; informe o valor já apurado em Proventos tributáveis, com seu DSR, sem repetir as comissões.");
        if (complementoComissoes > 0m)
            observacoes.Add($"A remuneração variável ficou abaixo da garantia de {Formato.Moeda(pisoComissoes)}; o complemento de {Formato.Moeda(complementoComissoes)} integra INSS, IRRF e FGTS.");
        if (r.OutrosProventos > 0m)
            observacoes.Add("Os outros proventos tributáveis entram no INSS, no IRRF e no FGTS. Comissões informadas nesse campo já devem incluir o DSR e não podem ser repetidas no campo Comissões do mês.");
        if (r.Premios > 0m)
            observacoes.Add("Os prêmios por desempenho superior ao esperado não entram no INSS nem no FGTS (CLT, art. 457, §§ 2º e 4º), mas têm IRRF e entram na base da pensão. Valores pagos todo mês ou sem ligação com o desempenho podem ser considerados salário: nesse caso, informe-os nos proventos tributáveis.");
        if (r.ProventosNaoTributaveis > 0m)
            observacoes.Add("Os proventos não tributáveis, como ajuda de custo, diárias de viagem e reembolsos de despesas, só somam no líquido: ficam fora do INSS, do IRRF, do FGTS, da pensão e do limite do salário-família.");
        if (r.PrevidenciaComplementar > 0m)
            observacoes.Add(MemoriaTributaria.ObservacaoPrevidenciaMensal);
        if (r.CustoValeTransporte > 0m)
            observacoes.Add("O vale-transporte é descontado em até 6% do salário-base, sem os adicionais, e não entra no INSS, no IRRF nem no FGTS.");
        if (r.Adiantamento > 0m || r.OutrosDescontos > 0m)
            observacoes.Add("O adiantamento e os descontos sem incidência saem do líquido, sem reduzir o INSS, o IRRF nem o FGTS.");
        if (r.Filhos > 0)
            observacoes.Add("O salário-família considera a remuneração do mês e uma cota por filho de até 14 anos ou inválido; nos meses de admissão e desligamento, use a calculadora de salário-família, que faz a cota proporcional.");
        observacoes.Add("O IRRF e o INSS são os da folha mensal; férias e 13º pagos no mês têm cálculo próprio, nas calculadoras de férias e de 13º.");

        return new DemonstrativoDto(
            "Holerite do mês",
            $"Competência {Formato.Competencia(tabelas.Competencia)}",
            [
                new("Líquido a receber", Formato.Moeda(liquido), "Proventos menos descontos"),
                new("Remuneração do mês", Formato.Moeda(remuneracao), "Base do INSS e do FGTS"),
                new("INSS + IRRF", Formato.Moeda(inss.Valor + irrf.Imposto), $"INSS de {Formato.Moeda(inss.Valor)} e IRRF de {Formato.Moeda(irrf.Imposto)}"),
                new("FGTS (8%)", Formato.Moeda(fgts), "Depositado pelo empregador")
            ],
            proventos,
            descontos,
            informativos,
            memoria,
            observacoes);
    }

    private static List<FormulaDto> FormulasRemuneracao(SimularHoleriteRequest r, HorasDoMes horas, decimal salarioMinimo, decimal insalubridade, decimal periculosidade, bool aplicaPericulosidade,
        decimal faltas, decimal descansos, decimal atrasos, decimal tributaveis, decimal remuneracao)
    {
        var formulas = new List<FormulaDto>();
        if (r.Insalubridade != GrauInsalubridade.Nenhum)
            formulas.Add(new($"Insalubridade ({NomeGrau(r.Insalubridade)})", $"{Formato.Moeda(salarioMinimo)} (salário mínimo) x {Formato.PercentualCurto((int)r.Insalubridade)} = {Formato.Moeda(insalubridade)}"));
        if (r.Periculosidade)
            formulas.Add(new("Periculosidade", $"{Formato.Moeda(r.Salario)} (salário) x 30% = {Formato.Moeda(periculosidade)}"));
        var adicional = aplicaPericulosidade ? periculosidade : insalubridade;
        var baseTexto = adicional > 0m ? $"({Formato.Moeda(r.Salario)} + {Formato.Moeda(adicional)} de adicional)" : Formato.Moeda(r.Salario);
        formulas.Add(new("Valor da hora normal", $"{baseTexto} ÷ {Formato.Numero(r.Divisor)} horas = {horas.ValorHora.ToString("C4", Formato.Cultura)} por hora"));
        formulas.AddRange(DemonstrativoHoras.Formulas(horas));
        if (faltas > 0m)
            formulas.Add(new("Faltas", $"{baseTexto} ÷ 30 x {Formato.Dias(r.Faltas)} = {Formato.Moeda(faltas)}"));
        if (descansos > 0m)
            formulas.Add(new("DSR perdido", $"{baseTexto} ÷ 30 x {Formato.Dias(r.DescansosPerdidos)} = {Formato.Moeda(descansos)}"));
        if (atrasos > 0m)
            formulas.Add(new("Atrasos", $"{horas.ValorHora.ToString("C4", Formato.Cultura)} x {Formato.Horas(r.HorasAtraso)} = {Formato.Moeda(atrasos)}"));
        var abatimentos = faltas + descansos + atrasos;
        formulas.Add(new("Remuneração do mês", abatimentos > 0m
            ? $"{Formato.Moeda(tributaveis)} (proventos tributáveis) - {Formato.Moeda(abatimentos)} (faltas e atrasos) = {Formato.Moeda(remuneracao)}"
            : $"{Formato.Moeda(tributaveis)} (proventos tributáveis)"));
        return formulas;
    }

    private static string NomeGrau(GrauInsalubridade grau) => grau switch
    {
        GrauInsalubridade.Minimo => "grau mínimo",
        GrauInsalubridade.Medio => "grau médio",
        _ => "grau máximo"
    };
}
