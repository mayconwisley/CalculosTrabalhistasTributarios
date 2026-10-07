using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Complemento do 13º por remuneração variável: compara o 13º pago em dezembro, com a média até novembro, ao 13º revisto
/// com as variáveis de dezembro (Decreto 57.155/1965, art. 2º). O INSS é recalculado sobre o 13º total com a tabela de
/// dezembro; o IRRF segue a opção escolhida quando o complemento é pago no ano seguinte.
/// </summary>
public sealed class SimularComplementoDecimoTerceiroUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularComplementoDecimoTerceiroRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularComplementoDecimoTerceiroRequest r, CancellationToken cancellationToken)
    {
        if (r.Dependentes is < 0 or > 99)
            return Erro.Validacao("Informe de 0 a 99 dependentes.");
        if (!Enum.IsDefined(r.Tributacao))
            return Erro.Validacao("Escolha como o IRRF do complemento é calculado.");
        if (r.Pagamento < r.Competencia)
            return Erro.Validacao($"O pagamento do complemento ({Formato.Competencia(r.Pagamento)}) não pode ser anterior à competência do 13º ({Formato.Competencia(r.Competencia)}).");
        var resultado = CalculadoraComplementoDecimoTerceiro.Calcular(r.Salario, r.MediaPaga, r.VariaveisAteNovembro, r.VariaveisDezembro, r.Avos);
        if (resultado.Falhou)
            return resultado.Erro;
        var c = resultado.Valor;
        var consulta = await tributacaoConsulta.ObterTabelasAsync(r.Competencia, cancellationToken);
        if (consulta.Falhou)
            return consulta.Erro;
        var tabelas = consulta.Valor;

        var inssPago = tabelas.CalcularInss(c.IntegralPago);
        var inssRevisado = tabelas.CalcularInss(c.IntegralRevisado);
        var diferencaInss = inssRevisado.Valor - inssPago.Valor;
        var irrfPago = tabelas.CalcularIrrf(c.IntegralPago, inssPago.Valor, r.Dependentes, tributacaoExclusiva: true);
        var irrfRevisado = tabelas.CalcularIrrf(c.IntegralRevisado, inssRevisado.Valor, r.Dependentes, tributacaoExclusiva: true);
        var diferencaRecalculo = irrfRevisado.Imposto - irrfPago.Imposto;
        var anoSeguinte = r.Pagamento.Year > r.Competencia.Year;
        var comoRra = anoSeguinte && r.Tributacao == TributacaoComplementoDecimoTerceiro.Rra && c.Diferenca > 0m;

        ApuracaoIrrfAcumulado? rra = null;
        if (comoRra)
        {
            var consultaPagamento = await tributacaoConsulta.ObterTabelasAsync(r.Pagamento, cancellationToken);
            if (consultaPagamento.Falhou)
                return consultaPagamento.Erro;
            // RRA de um mês: o 13º conta como um mês, e só a contribuição previdenciária do complemento é deduzida.
            rra = consultaPagamento.Valor.CalcularIrrfAcumulado(c.Diferenca, c.Diferenca - diferencaInss, 1m, aplicarReducao: true);
        }
        var irrf = rra?.Imposto ?? Math.Max(0m, diferencaRecalculo);
        var fgts = c.Diferenca > 0m ? CalculadoraTributacao.Arredondar(c.Diferenca * .08m) : 0m;
        var liquido = c.Diferenca - diferencaInss - irrf;
        var nomeIrrf = comoRra ? "RRA de 1 mês" : "recálculo do 13º";

        var destaques = new List<DestaqueDto>
        {
            new("Média usada em dezembro", Formato.Moeda(c.MediaPaga), "Na 2ª parcela"),
            new("Média final do ano", Formato.Moeda(c.MediaFinal), $"{Formato.Moeda(c.VariaveisAno)} ÷ {c.Avos}"),
            new(c.Diferenca >= 0m ? "Complemento bruto" : "Pago a maior", Formato.Moeda(Math.Abs(c.Diferenca)), Formato.Avos(c.Avos)),
            new(c.Diferenca >= 0m ? "Complemento líquido" : "Compensação", Formato.Moeda(c.Diferenca >= 0m ? liquido : -c.Diferenca),
                c.Diferenca >= 0m ? $"Após INSS e IRRF ({nomeIrrf})" : "Pode ser descontada do trabalhador")
        };

        var proventos = new List<VerbaDto>();
        var descontos = new List<VerbaDto>();
        var informativos = new List<VerbaDto>();
        if (c.Diferenca > 0m)
        {
            proventos.Add(new("Complemento do 13º (médias de variáveis)", Formato.Avos(c.Avos), c.Diferenca));
            if (diferencaInss > 0m) descontos.Add(new("INSS sobre o complemento", "Recalculado sobre o 13º total", diferencaInss));
            if (irrf > 0m) descontos.Add(new($"IRRF sobre o complemento ({nomeIrrf})", comoRra ? "Tabela do pagamento" : "Tabela de dezembro", irrf));
            informativos.Add(new("FGTS sobre o complemento", "8% do complemento", fgts));
        }
        else if (c.Diferenca < 0m)
        {
            descontos.Add(new("13º pago a maior (compensação)", "Média final menor", -c.Diferenca));
            if (diferencaInss < 0m) informativos.Add(new("INSS do 13º descontado a maior", "Recalculado sobre o 13º total", -diferencaInss));
            if (diferencaRecalculo < 0m) informativos.Add(new("IRRF do 13º retido a maior", "Recálculo do 13º", -diferencaRecalculo));
        }

        var memoria = new List<GrupoMemoriaDto>
        {
            new("Médias de variáveis", $"Diferença: {Formato.MoedaComSinal(c.Diferenca)}",
            [
                new("Variáveis do ano", $"{Formato.Moeda(c.VariaveisAteNovembro)} (até novembro) + {Formato.Moeda(c.VariaveisDezembro)} (dezembro) = {Formato.Moeda(c.VariaveisAno)}"),
                new("Média final", $"{Formato.Moeda(c.VariaveisAno)} ÷ {c.Avos} = {Formato.Moeda(c.MediaFinal)}"),
                new("13º pago em dezembro", $"({Formato.Moeda(c.Salario)} + {Formato.Moeda(c.MediaPaga)}) ÷ 12 × {c.Avos} = {Formato.Moeda(c.IntegralPago)}"),
                new("13º revisado", $"({Formato.Moeda(c.Salario)} + {Formato.Moeda(c.MediaFinal)}) ÷ 12 × {c.Avos} = {Formato.Moeda(c.IntegralRevisado)}"),
                new("Diferença", $"{Formato.Moeda(c.IntegralRevisado)} - {Formato.Moeda(c.IntegralPago)} = {Formato.MoedaComSinal(c.Diferenca)}")
            ]),
            new("INSS do 13º", $"Diferença: {Formato.MoedaComSinal(diferencaInss)}",
            [
                new("Em dezembro", $"INSS sobre {Formato.Moeda(c.IntegralPago)} = {Formato.Moeda(inssPago.Valor)}"),
                new("Revisado", $"INSS sobre {Formato.Moeda(c.IntegralRevisado)} = {Formato.Moeda(inssRevisado.Valor)}, tabela de {Formato.Competencia(tabelas.Competencia)}"),
                new("Diferença", $"{Formato.Moeda(inssRevisado.Valor)} - {Formato.Moeda(inssPago.Valor)} = {Formato.MoedaComSinal(diferencaInss)}")
            ])
        };
        if (rra is not null)
            memoria.Add(new GrupoMemoriaDto("IRRF do complemento (RRA de 1 mês)", $"IRRF: {Formato.Moeda(rra.Imposto)}",
            [
                new("Base", $"{Formato.Moeda(c.Diferenca)} (complemento) - {Formato.Moeda(diferencaInss)} (INSS) = {Formato.Moeda(rra.BaseCalculo)}"),
                new("Imposto", rra.Aliquota == 0m
                    ? $"{Formato.Moeda(rra.BaseCalculo)} está na faixa isenta da tabela de {Formato.Competencia(r.Pagamento)}: {Formato.Moeda(0m)}"
                    : $"{Formato.Moeda(rra.BaseCalculo)} × {Formato.Percentual(rra.Aliquota)} - {rra.ParcelaADeduzirMensal.ToString("#,##0.00###", Formato.Cultura)} = {Formato.Moeda(rra.ImpostoAntesReducao)}{(rra.Reducao > 0m ? $"; menos a redução de {Formato.Moeda(rra.Reducao)} = {Formato.Moeda(rra.Imposto)}" : "")}"),
                new("Comparação", $"Pelo recálculo do 13º, o IRRF seria de {Formato.Moeda(Math.Max(0m, diferencaRecalculo))}")
            ]));
        else
            memoria.Add(new GrupoMemoriaDto("IRRF do 13º recalculado", $"Diferença: {Formato.MoedaComSinal(diferencaRecalculo)}",
            [
                new("Em dezembro", $"IRRF sobre {Formato.Moeda(c.IntegralPago)} = {Formato.Moeda(irrfPago.Imposto)}"),
                new("Revisado", $"IRRF sobre {Formato.Moeda(c.IntegralRevisado)} = {Formato.Moeda(irrfRevisado.Imposto)}, tabela de {Formato.Competencia(tabelas.Competencia)}"),
                new("Diferença", $"{Formato.Moeda(irrfRevisado.Imposto)} - {Formato.Moeda(irrfPago.Imposto)} = {Formato.MoedaComSinal(diferencaRecalculo)}")
            ]));

        var observacoes = new List<string>
        {
            "Para quem recebe remuneração variável, a 2ª parcela usa a média das variáveis até novembro. Até 10 de janeiro, o 13º é refeito com as variáveis de dezembro, e a diferença é paga ou compensada (Decreto 57.155/1965, art. 2º). A média final divide as variáveis do ano pelos avos; confira a norma coletiva, que pode prever outro critério.",
            $"O INSS do complemento é a diferença entre o INSS sobre o 13º revisado e o já descontado, pela tabela de {Formato.Competencia(tabelas.Competencia)}."
        };
        observacoes.Add(!anoSeguinte
            ? "Como o complemento é pago no mesmo ano, o IRRF é recalculado sobre o 13º total pela tabela de dezembro, deduzido o imposto já retido (IN RFB 1.500/2014, art. 13, § 3º)."
            : comoRra
            ? "O complemento pago no ano seguinte foi tratado como RRA de um mês, como o eSocial orienta para diferenças de 13º de ano anterior, informadas no período de referência de dezembro (Manual de Orientação do eSocial; IN RFB 1.500/2014, art. 37, § 1º). A IN RFB 1.500/2014, art. 13, § 3º, prevê o recálculo do 13º total: a memória mostra o valor por esse critério; confira com a folha."
            : "O IRRF foi recalculado sobre o 13º total pela tabela de dezembro, deduzido o imposto já retido (IN RFB 1.500/2014, art. 13, § 3º). O eSocial orienta informar diferenças de 13º de ano anterior como RRA de um mês; confira o critério da folha.");
        if (anoSeguinte && (r.Pagamento.Year > r.Competencia.Year + 1 || r.Pagamento.Month > 1))
            observacoes.Insert(0, $"O pagamento em {Formato.Competencia(r.Pagamento)} passa do prazo do Decreto 57.155/1965, até 10 de janeiro. A diferença paga com atraso pode ter outros efeitos, como correção e encargos.");
        if (c.Diferenca < 0m)
            observacoes.Add("A média final ficou menor que a usada em dezembro: o 13º foi pago a maior. A compensação depende do que prevê o decreto e a norma coletiva; o INSS e o IRRF recalculados mostram o que foi descontado a mais.");

        return new DemonstrativoDto("Complemento do 13º (médias de variáveis)",
            $"13º de {r.Competencia.Year} • pagamento em {Formato.Competencia(r.Pagamento)} • {Formato.Avos(c.Avos)}",
            destaques, proventos, descontos, informativos, memoria, observacoes,
            RotuloProventos: "Complemento", RotuloResultado: c.Diferenca >= 0m ? "Complemento líquido a receber" : "Valor a compensar",
            Comparativo: new TabelaComparativaDto("13º de dezembro e 13º revisado", ["Dezembro", "Revisado", "Diferença"],
            [
                new("Média de variáveis", [Formato.Moeda(c.MediaPaga), Formato.Moeda(c.MediaFinal), Formato.MoedaComSinal(c.MediaFinal - c.MediaPaga)]),
                new("13º integral", [Formato.Moeda(c.IntegralPago), Formato.Moeda(c.IntegralRevisado), Formato.MoedaComSinal(c.Diferenca)], true),
                new("INSS", [Formato.Moeda(inssPago.Valor), Formato.Moeda(inssRevisado.Valor), Formato.MoedaComSinal(diferencaInss)]),
                new("IRRF (recálculo)", [Formato.Moeda(irrfPago.Imposto), Formato.Moeda(irrfRevisado.Imposto), Formato.MoedaComSinal(diferencaRecalculo)])
            ]));
    }
}
