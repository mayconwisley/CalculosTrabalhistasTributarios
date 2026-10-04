using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Declaração anual do imposto de renda a partir do ano-calendário 2026: os modelos completo e simplificado, cada um com
/// a redução anual (Lei 9.250/1995, art. 11-A), o de menor imposto e a tributação mínima das altas rendas (arts. 16-A e
/// 16-B), com o redutor pela alíquota efetiva da empresa que pagou os dividendos.
/// </summary>
public sealed class SimularIrpfAnualUseCase : ISimularDemonstrativoUseCase<SimularIrpfAnualRequest>
{
    private sealed record Modelo(string Nome, decimal Deducoes, decimal Base, decimal ImpostoTabela, decimal Reducao)
    {
        public decimal Devido => ImpostoTabela - Reducao;
    }

    // O cálculo não consulta o banco: a tarefa só cumpre o contrato assíncrono da interface.
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularIrpfAnualRequest r, CancellationToken cancellationToken) => Task.FromResult(Simular(r));

    private static Result<DemonstrativoDto> Simular(SimularIrpfAnualRequest r)
    {
        if (r.Ano < ImpostoRendaAnual.PrimeiroAno)
            return Erro.Validacao($"A calculadora segue a Lei 15.270/2025, que vale desde o ano-calendário {ImpostoRendaAnual.PrimeiroAno} (declaração de {ImpostoRendaAnual.PrimeiroAno + 1}).");
        if (new[] { r.RendimentosTributaveis, r.PrevidenciaOficial, r.DespesasMedicas, r.Instrucao, r.PrevidenciaComplementar, r.PensaoAlimenticia, r.ImpostoPago, r.Dividendos, r.IrrfDividendos, r.OutrosRendimentos, r.ImpostoExclusivo, r.AliquotaEfetivaEmpresa }.Any(valor => valor < 0m) || r.Dependentes < 0)
            return Erro.Validacao("Os valores e a quantidade de dependentes não podem ser negativos.");
        if (r.AliquotaEfetivaEmpresa > 100m)
            return Erro.Validacao("A alíquota efetiva da empresa deve ficar entre 0 e 100%.");

        var observacoes = new List<string>
        {
            "O 13º salário e a PLR têm tributação exclusiva na fonte e ficam fora dos rendimentos tributáveis do ajuste; eles entram na tributação mínima, nos outros rendimentos.",
            "A redução anual usa os rendimentos tributáveis brutos e vale nos dois modelos: até R$ 60.000,00 o imposto é zerado (até R$ 2.694,15) e até R$ 88.200,00 a redução diminui em linha (Lei 9.250/1995, art. 11-A)."
        };

        // Modelo completo: as deduções legais, com os limites da instrução e da previdência complementar.
        var limiteInstrucao = ImpostoRendaAnual.LimiteInstrucaoPorPessoa * (1 + r.Dependentes);
        var instrucao = Math.Min(r.Instrucao, limiteInstrucao);
        if (r.Instrucao > limiteInstrucao)
            observacoes.Add($"A instrução foi limitada a {Formato.Moeda(limiteInstrucao)}: {Formato.Moeda(ImpostoRendaAnual.LimiteInstrucaoPorPessoa)} por pessoa, para o titular e {r.Dependentes} dependente(s). O limite é individual: o que uma pessoa gasta a mais não aproveita o de outra.");
        var limitePgbl = CalculadoraTributacao.Arredondar(r.RendimentosTributaveis * ImpostoRendaAnual.LimitePrevidenciaComplementar / 100m);
        var pgbl = Math.Min(r.PrevidenciaComplementar, limitePgbl);
        if (r.PrevidenciaComplementar > limitePgbl)
            observacoes.Add($"A previdência complementar foi limitada a 12% dos rendimentos tributáveis, {Formato.Moeda(limitePgbl)}.");
        var dependentes = r.Dependentes * ImpostoRendaAnual.DeducaoPorDependente;
        var deducoes = r.PrevidenciaOficial + dependentes + instrucao + r.DespesasMedicas + pgbl + r.PensaoAlimenticia;
        var completo = Montar("Completa", r.RendimentosTributaveis, deducoes);
        var simplificado = Montar("Simplificada", r.RendimentosTributaveis, ImpostoRendaAnual.DescontoSimplificado(r.RendimentosTributaveis));
        var escolhido = simplificado.Devido < completo.Devido ? simplificado : completo;

        // Tributação mínima: todos os rendimentos do ano, inclusive isentos e exclusivos, acima de 600 mil.
        var rendimentosTotais = r.RendimentosTributaveis + r.Dividendos + r.OutrosRendimentos;
        var deducoesMinima = escolhido.Devido + r.ImpostoExclusivo;
        decimal Minima(decimal rendimentos) => Math.Max(0m, CalculadoraTributacao.Arredondar(rendimentos * ImpostoRendaAnual.AliquotaMinima(rendimentos) / 100m) - deducoesMinima);
        var aliquotaMinima = ImpostoRendaAnual.AliquotaMinima(rendimentosTotais);
        var brutoMinima = CalculadoraTributacao.Arredondar(rendimentosTotais * aliquotaMinima / 100m);
        var minimaAntesRedutor = Minima(rendimentosTotais);

        // Redutor: a soma das alíquotas efetivas da empresa e da pessoa sobre os dividendos não passa da nominal (art. 16-B).
        var redutor = 0m;
        var aliquotaPessoa = 0m;
        if (minimaAntesRedutor > 0m && r.Dividendos > 0m && r.AliquotaEfetivaEmpresa > 0m)
        {
            var acrescimo = minimaAntesRedutor - Minima(rendimentosTotais - r.Dividendos);
            aliquotaPessoa = acrescimo / r.Dividendos * 100m;
            var excesso = r.AliquotaEfetivaEmpresa + aliquotaPessoa - (int)r.AliquotaNominal;
            if (excesso > 0m)
                redutor = Math.Min(minimaAntesRedutor, CalculadoraTributacao.Arredondar(r.Dividendos * excesso / 100m));
        }
        var minimaDevida = minimaAntesRedutor - redutor;
        var saldoMinima = minimaDevida - r.IrrfDividendos;
        var saldoDeclaracao = escolhido.Devido - r.ImpostoPago;
        var saldo = saldoDeclaracao + saldoMinima;

        if (rendimentosTotais > ImpostoRendaAnual.LimiteTributacaoMinima)
        {
            observacoes.Add("Na tributação mínima não entram ganhos de capital (salvo os de bolsa), heranças e doações, poupança, LCI, LCA, CRI, CRA, debêntures incentivadas, FII e Fiagro com os requisitos da lei, indenizações por acidente e danos, a parcela isenta da atividade rural, os rendimentos isentos por moléstia grave e os lucros de 2025 aprovados até 31/12/2025 e pagos até 2028 (Lei 9.250/1995, art. 16-A, § 1º). Não os inclua nos outros rendimentos.");
            if (r.Dividendos > 0m && r.AliquotaEfetivaEmpresa == 0m)
                observacoes.Add("Sem a alíquota efetiva da empresa, o redutor não foi calculado. Ele depende das demonstrações da empresa: IRPJ e CSLL devidos divididos pelo lucro contábil antes dos tributos (art. 16-B).");
        }
        else if (r.IrrfDividendos > 0m)
            observacoes.Add("Com rendimentos de até R$ 600 mil no ano não há tributação mínima: a retenção de 10% sobre os dividendos volta como restituição na declaração.");
        observacoes.Add("As regras da Lei 15.270/2025 ainda serão detalhadas pelo programa da declaração de 2027 e por instruções da Receita, como a ordem entre a redução anual e o limite de 6% das doações incentivadas, que não estão nesta simulação.");

        var comparativo = new TabelaComparativaDto("Declaração completa ou simplificada", ["Completa", "Simplificada"],
        [
            new("Rendimentos tributáveis", [Formato.Moeda(r.RendimentosTributaveis), Formato.Moeda(r.RendimentosTributaveis)]),
            new("Deduções legais ou desconto simplificado", [Formato.Moeda(completo.Deducoes), Formato.Moeda(simplificado.Deducoes)]),
            new("Base de cálculo", [Formato.Moeda(completo.Base), Formato.Moeda(simplificado.Base)]),
            new("Imposto pela tabela anual", [Formato.Moeda(completo.ImpostoTabela), Formato.Moeda(simplificado.ImpostoTabela)]),
            new("Redução anual (art. 11-A)", [Formato.Moeda(completo.Reducao), Formato.Moeda(simplificado.Reducao)]),
            new("Imposto devido", [Formato.Moeda(completo.Devido), Formato.Moeda(simplificado.Devido)], Destaque: true)
        ]);

        var informativos = new List<VerbaDto>
        {
            new($"Imposto devido na declaração {escolhido.Nome.ToLowerInvariant()}", "", escolhido.Devido),
            new("Imposto já retido ou pago", "", r.ImpostoPago),
            new("Rendimentos para a tributação mínima", "", rendimentosTotais)
        };
        if (rendimentosTotais > ImpostoRendaAnual.LimiteTributacaoMinima)
        {
            informativos.Add(new("Tributação mínima antes das deduções", Formato.Percentual(aliquotaMinima), brutoMinima));
            informativos.Add(new("Tributação mínima devida, com as deduções e o redutor", "", minimaDevida));
        }
        if (r.IrrfDividendos > 0m)
            informativos.Add(new("IRRF sobre dividendos compensado", "10%", r.IrrfDividendos));

        var memoria = new List<GrupoMemoriaDto>
        {
            MemoriaModelo(completo, r, instrucao, pgbl, dependentes),
            MemoriaModelo(simplificado, r, instrucao, pgbl, dependentes),
            new("Modelo escolhido", $"Imposto: {Formato.Moeda(escolhido.Devido)}",
            [
                new("Menor imposto", completo.Devido == simplificado.Devido
                    ? $"Os dois modelos resultam em {Formato.Moeda(escolhido.Devido)}."
                    : $"Declaração {escolhido.Nome.ToLowerInvariant()}: {Formato.Moeda(escolhido.Devido)}, contra {Formato.Moeda((escolhido == completo ? simplificado : completo).Devido)}.")
            ])
        };
        if (rendimentosTotais > ImpostoRendaAnual.LimiteTributacaoMinima)
            memoria.Add(MemoriaMinima(r, rendimentosTotais, aliquotaMinima, brutoMinima, escolhido.Devido, minimaAntesRedutor, aliquotaPessoa, redutor, minimaDevida, saldoMinima));
        memoria.Add(new("Saldo da declaração", Saldo(saldo),
        [
            new("Imposto de renda", $"{Formato.Moeda(escolhido.Devido)} (devido) - {Formato.Moeda(r.ImpostoPago)} (retido ou pago) = {Formato.Moeda(saldoDeclaracao)}"),
            new("Tributação mínima", $"{Formato.Moeda(minimaDevida)} (devida) - {Formato.Moeda(r.IrrfDividendos)} (IRRF sobre dividendos) = {Formato.Moeda(saldoMinima)}"),
            new("Saldo", $"{Formato.Moeda(saldoDeclaracao)} + {Formato.Moeda(saldoMinima)} = {Formato.Moeda(saldo)}: {char.ToLowerInvariant(Saldo(saldo)[0])}{Saldo(saldo)[1..]}")
        ]));

        var rendaTotal = rendimentosTotais;
        var impostoTotal = escolhido.Devido + minimaDevida + r.ImpostoExclusivo;
        return new DemonstrativoDto(
            "IRPF anual",
            $"Ano-calendário {r.Ano} • declaração de {r.Ano + 1}",
            [
                new("Imposto devido", Formato.Moeda(escolhido.Devido), $"Declaração {escolhido.Nome.ToLowerInvariant()}"),
                new("Saldo da declaração", Formato.Moeda(Math.Abs(saldo)), saldo > 0m ? "A pagar" : saldo < 0m ? "A restituir" : "Sem saldo"),
                new("Tributação mínima", Formato.Moeda(minimaDevida), rendimentosTotais > ImpostoRendaAnual.LimiteTributacaoMinima ? $"Alíquota de {Formato.Percentual(aliquotaMinima)}" : "Rendimentos até R$ 600 mil"),
                new("Alíquota efetiva", rendaTotal > 0m ? Formato.Percentual(impostoTotal / rendaTotal * 100m) : "0,00%", "Imposto sobre todos os rendimentos")
            ],
            [],
            [],
            informativos,
            memoria,
            observacoes,
            Comparativo: comparativo);
    }

    private static Modelo Montar(string nome, decimal rendimentos, decimal deducoes)
    {
        var baseCalculo = Math.Max(0m, rendimentos - deducoes);
        var imposto = ImpostoRendaAnual.ImpostoPelaTabela(baseCalculo);
        return new Modelo(nome, deducoes, baseCalculo, imposto, ImpostoRendaAnual.Reducao(rendimentos, imposto));
    }

    private static string Saldo(decimal saldo) => saldo > 0m ? $"A pagar {Formato.Moeda(saldo)}" : saldo < 0m ? $"A restituir {Formato.Moeda(-saldo)}" : "Sem saldo";

    private static GrupoMemoriaDto MemoriaModelo(Modelo modelo, SimularIrpfAnualRequest r, decimal instrucao, decimal pgbl, decimal dependentes)
    {
        var formulas = new List<FormulaDto>();
        if (modelo.Nome == "Completa")
        {
            var partes = new List<string>();
            if (r.PrevidenciaOficial > 0m) partes.Add($"{Formato.Moeda(r.PrevidenciaOficial)} (previdência oficial)");
            if (dependentes > 0m) partes.Add($"{r.Dependentes} x {Formato.Moeda(ImpostoRendaAnual.DeducaoPorDependente)} (dependentes)");
            if (instrucao > 0m) partes.Add($"{Formato.Moeda(instrucao)} (instrução)");
            if (r.DespesasMedicas > 0m) partes.Add($"{Formato.Moeda(r.DespesasMedicas)} (despesas médicas)");
            if (pgbl > 0m) partes.Add($"{Formato.Moeda(pgbl)} (previdência complementar)");
            if (r.PensaoAlimenticia > 0m) partes.Add($"{Formato.Moeda(r.PensaoAlimenticia)} (pensão alimentícia)");
            formulas.Add(new("Deduções legais", partes.Count == 0 ? "Nenhuma dedução informada" : $"{string.Join(" + ", partes)} = {Formato.Moeda(modelo.Deducoes)}"));
        }
        else
            formulas.Add(new("Desconto simplificado", $"20% de {Formato.Moeda(r.RendimentosTributaveis)}, até {Formato.Moeda(ImpostoRendaAnual.LimiteSimplificado)} = {Formato.Moeda(modelo.Deducoes)}"));
        formulas.Add(new("Base de cálculo", $"{Formato.Moeda(r.RendimentosTributaveis)} - {Formato.Moeda(modelo.Deducoes)} = {Formato.Moeda(modelo.Base)}"));
        var faixa = ImpostoRendaAnual.Faixa(modelo.Base);
        formulas.Add(new("Imposto pela tabela anual", faixa.Aliquota == 0m
            ? $"{Formato.Moeda(modelo.Base)} está na faixa isenta: {Formato.Moeda(0m)}"
            : $"{Formato.Moeda(modelo.Base)} x {Formato.Percentual(faixa.Aliquota)} - {Formato.Moeda(faixa.Deducao)} = {Formato.Moeda(modelo.ImpostoTabela)}"));
        formulas.Add(new("Redução anual", r.RendimentosTributaveis <= ImpostoRendaAnual.LimiteIsencaoReducao
            ? $"Rendimentos de até {Formato.Moeda(ImpostoRendaAnual.LimiteIsencaoReducao)}: redução de até {Formato.Moeda(ImpostoRendaAnual.ReducaoMaxima)}, limitada ao imposto = {Formato.Moeda(modelo.Reducao)}"
            : r.RendimentosTributaveis <= ImpostoRendaAnual.LimiteReducao
                ? $"{Formato.Moeda(ImpostoRendaAnual.ReducaoValorBase)} - 0,095575 x {Formato.Moeda(r.RendimentosTributaveis)}, limitada ao imposto = {Formato.Moeda(modelo.Reducao)}"
                : $"Rendimentos acima de {Formato.Moeda(ImpostoRendaAnual.LimiteReducao)}: sem redução"));
        formulas.Add(new("Imposto devido", $"{Formato.Moeda(modelo.ImpostoTabela)} - {Formato.Moeda(modelo.Reducao)} = {Formato.Moeda(modelo.Devido)}"));
        return new GrupoMemoriaDto($"Declaração {modelo.Nome.ToLowerInvariant()}", $"Imposto: {Formato.Moeda(modelo.Devido)}", formulas);
    }

    private static GrupoMemoriaDto MemoriaMinima(SimularIrpfAnualRequest r, decimal rendimentos, decimal aliquota, decimal bruto, decimal irpf, decimal antesRedutor, decimal aliquotaPessoa,
        decimal redutor, decimal devida, decimal saldo)
    {
        var formulas = new List<FormulaDto>
        {
            new("Rendimentos do ano", $"{Formato.Moeda(r.RendimentosTributaveis)} (tributáveis) + {Formato.Moeda(r.Dividendos)} (dividendos) + {Formato.Moeda(r.OutrosRendimentos)} (outros) = {Formato.Moeda(rendimentos)}"),
            new("Alíquota", rendimentos >= ImpostoRendaAnual.RendaAliquotaMaxima
                ? $"Rendimentos de {Formato.Moeda(ImpostoRendaAnual.RendaAliquotaMaxima)} ou mais: {Formato.Percentual(aliquota)}"
                : $"{Formato.Moeda(rendimentos)} ÷ 60.000 - 10 = {aliquota.ToString("N4", Formato.Cultura)}%"),
            new("Tributação mínima bruta", $"{Formato.Moeda(rendimentos)} x {aliquota.ToString("N4", Formato.Cultura)}% = {Formato.Moeda(bruto)}"),
            new("Deduções", $"{Formato.Moeda(bruto)} - {Formato.Moeda(irpf)} (IRPF devido) - {Formato.Moeda(r.ImpostoExclusivo)} (imposto exclusivo ou definitivo) = {Formato.Moeda(antesRedutor)}")
        };
        if (r.Dividendos > 0m && r.AliquotaEfetivaEmpresa > 0m)
        {
            formulas.Add(new("Alíquota efetiva da pessoa sobre os dividendos", $"acréscimo da tributação mínima pelos dividendos ÷ {Formato.Moeda(r.Dividendos)} = {aliquotaPessoa.ToString("N4", Formato.Cultura)}%"));
            formulas.Add(new("Redutor", redutor > 0m
                ? $"{Formato.Moeda(r.Dividendos)} x ({Formato.Percentual(r.AliquotaEfetivaEmpresa)} + {aliquotaPessoa.ToString("N4", Formato.Cultura)}% - {(int)r.AliquotaNominal}%) = {Formato.Moeda(redutor)}"
                : $"{Formato.Percentual(r.AliquotaEfetivaEmpresa)} (empresa) + {aliquotaPessoa.ToString("N4", Formato.Cultura)}% (pessoa) não passa de {(int)r.AliquotaNominal}%: sem redutor"));
        }
        formulas.Add(new("Tributação mínima devida", $"{Formato.Moeda(antesRedutor)} - {Formato.Moeda(redutor)} (redutor) = {Formato.Moeda(devida)}"));
        formulas.Add(new("Compensação do IRRF sobre dividendos", $"{Formato.Moeda(devida)} - {Formato.Moeda(r.IrrfDividendos)} = {Formato.Moeda(saldo)}"));
        return new GrupoMemoriaDto("Tributação mínima das altas rendas", $"Devida: {Formato.Moeda(devida)}", formulas);
    }
}
