
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>
/// Passo a passo de cada iteração do cálculo da pensão. A tela e o relatório PDF usam o mesmo texto, para explicarem o
/// cálculo da mesma forma.
/// </summary>
public static class MemoriaCalculoPensao
{
    /// <summary>
    /// Como a pensão foi obtida e para que servem os rendimentos de quem paga. Nas bases que não dependem do IRRF, o valor
    /// bruto, o INSS e o IRRF só mostram o efeito da pensão no salário de quem paga.
    /// </summary>
    public static string Explicacao(SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada, Func<decimal, string> moeda, Func<decimal, string> percentual)
    {
        var aplicada = simulacao.Aplicada;
        var pensoes = aplicada.Detalhes[^1].Beneficiarios;
        if (pensoes.Count > 1)
        {
            var partes = string.Join("; ", pensoes.Select(pensao => $"{pensao.Nome}, {moeda(pensao.Pensao)} ({pensao.Descrever(moeda, percentual)})"));
            var ordem = entrada.Sucessiva ? "Cada pensão foi calculada depois de descontar as anteriores da base." : "Todas as pensões incidem sobre a mesma base.";
            var irrf = pensoes.Any(pensao => pensao.Regra.Base == BasePensao.RendimentosLiquidos)
                ? "As pensões são deduzidas da base do IRRF na modalidade normal, e as que incidem sobre os rendimentos líquidos dependem desse IRRF; por isso o cálculo é feito em iterações. No desconto simplificado, elas não são deduzidas."
                : "O valor bruto, o INSS e o IRRF mostram o efeito das pensões no salário de quem paga: elas são deduzidas da base do IRRF na modalidade normal, e não no desconto simplificado.";
            return $"As {pensoes.Count} pensões somam {moeda(aplicada.Pensao)} na modalidade {NomeModalidade(aplicada)}: {partes}. {ordem} {irrf}";
        }

        var regra = pensoes[0].Regra;
        var basePensao = pensoes[0].Base;
        return regra.Base switch
        {
            BasePensao.RendimentosLiquidos =>
                $"A pensão é de {percentual(regra.Percentual)} dos rendimentos líquidos de quem paga, ou seja, do valor bruto menos o INSS e o IRRF: {moeda(aplicada.Pensao)} na modalidade {NomeModalidade(aplicada)}. " +
                "Como a pensão também é deduzida da base do IRRF na modalidade normal, o cálculo é feito em iterações. No desconto simplificado, ela não é deduzida.",
            BasePensao.RendimentosBrutos =>
                $"A pensão é de {percentual(regra.Percentual)} dos rendimentos brutos: {percentual(regra.Percentual)} de {moeda(basePensao)} = {moeda(aplicada.Pensao)}. Ela não depende do INSS nem do IRRF. " +
                "O INSS e o IRRF mostram o efeito da pensão no salário de quem paga: ela é deduzida da base do IRRF na modalidade normal, e não no desconto simplificado.",
            BasePensao.SalarioMinimo =>
                $"A pensão não depende dos rendimentos de quem paga: {percentual(regra.Percentual)} de {moeda(basePensao)} (salário mínimo de {entrada.Competencia:MM/yyyy}) = {moeda(aplicada.Pensao)}. " +
                "O valor bruto, o INSS e o IRRF servem só para mostrar o efeito da pensão no salário de quem paga: ela é deduzida da base do IRRF na modalidade normal, e não no desconto simplificado.",
            _ =>
                $"A pensão é o valor fixo de {moeda(regra.Valor)} e não depende dos rendimentos de quem paga. " +
                "O valor bruto, o INSS e o IRRF servem só para mostrar o efeito da pensão no salário de quem paga: ela é deduzida da base do IRRF na modalidade normal, e não no desconto simplificado."
        };
    }

    /// <summary>O IRRF de quem paga com e sem a pensão, e quanto a dedução economiza.</summary>
    public static string EfeitoNoIrrf(SimulacaoPensaoDto simulacao, Func<decimal, string> moeda) =>
        simulacao.EconomiaIrrf > 0m
            ? $"Sem a pensão, o IRRF de quem paga seria de {moeda(simulacao.ImpostoSemPensao)}; com a dedução da pensão, é de {moeda(simulacao.Aplicada.Imposto)}, uma economia de {moeda(simulacao.EconomiaIrrf)}."
            : $"A pensão não reduz o IRRF de quem paga, que é de {moeda(simulacao.Aplicada.Imposto)} com ou sem ela.";

    /// <summary>
    /// Faixa do INSS em que a base termina: "até a faixa de 14%" no regime progressivo, desde 03/2020, em que cada parte da
    /// base paga a alíquota da sua faixa; antes, "alíquota de 9% sobre toda a base". Vazio sem base de INSS.
    /// </summary>
    public static string FaixaInss(SimulacaoPensaoDto simulacao)
    {
        if (simulacao.FaixasInss.Count == 0)
            return string.Empty;
        var aliquota = Formato.PercentualCurto(simulacao.FaixasInss[^1].Aliquota);
        return simulacao.InssProgressivo ? $"até a faixa de {aliquota}" : $"alíquota de {aliquota} sobre toda a base";
    }

    /// <summary>
    /// Faixa da tabela progressiva do IRRF em que a base ficou. Na faixa tributada, o imposto ainda pode ser zero pela
    /// redução mensal ou por não passar do limite de retenção.
    /// </summary>
    public static string FaixaIrrf(decimal aliquota, decimal imposto) =>
        aliquota == 0m ? "faixa isenta"
        : imposto == 0m ? $"faixa de {Formato.PercentualCurto(aliquota)}, sem IRRF a reter"
        : $"faixa de {Formato.PercentualCurto(aliquota)}";

    /// <summary>Faixas do INSS e do IRRF, com e sem a pensão, em uma frase para os relatórios.</summary>
    public static string Faixas(SimulacaoPensaoDto simulacao, Func<decimal, string> moeda)
    {
        var aplicada = simulacao.Aplicada.Detalhes[^1];
        var inss = simulacao.FaixasInss.Count == 0
            ? "sem base de INSS"
            : $"INSS de {moeda(simulacao.ValorInss)}, {FaixaInss(simulacao)}";
        return $"Faixas aplicadas: {inss}; IRRF com a pensão na {FaixaIrrf(aplicada.Aliquota, aplicada.Imposto)} e sem a pensão na {FaixaIrrf(simulacao.AliquotaIrrfSemPensao, simulacao.ImpostoSemPensao)}.";
    }

    /// <summary>Complemento de "na modalidade ...": "normal (deduções legais)" ou "de desconto simplificado".</summary>
    public static string NomeModalidade(ModalidadePensaoDto modalidade) => modalidade.DeduzPensao ? "normal (deduções legais)" : "de desconto simplificado";

    public static IReadOnlyList<FormulaDto> Iteracao(ModalidadePensaoDto modalidade, IteracaoPensaoDto iteracao, EntradaPensaoDto entrada, decimal valorInss, Func<decimal, string> moeda, Func<decimal, string> percentual)
    {
        var pensoes = iteracao.Beneficiarios;
        var varias = pensoes.Count > 1;
        var rendimentos = entrada.ValorBruto - entrada.OutrosDescontos;
        var rotuloRendimentos = entrada.OutrosDescontos > 0m ? "rendimentos menos outros descontos" : "rendimentos";
        var termosBase = modalidade.DeduzPensao
            ? $"{moeda(rendimentos)} ({rotuloRendimentos}) - {moeda(modalidade.DeducoesBase)} ({modalidade.RotuloDeducoes}) - {moeda(iteracao.PensaoDeduzida)} ({(varias ? "pensões" : "pensão")})"
            : $"{moeda(rendimentos)} ({rotuloRendimentos}) - {moeda(modalidade.DeducoesBase)} ({modalidade.RotuloDeducoes})";
        // A base não fica negativa: quando as deduções superam os rendimentos, ela é zero.
        var baseIrrf = rendimentos - modalidade.DeducoesBase - iteracao.PensaoDeduzida < 0m
            ? $"{termosBase} = {moeda(iteracao.BaseIrrf)} (as deduções superam os rendimentos)"
            : $"{termosBase} = {moeda(iteracao.BaseIrrf)}";
        if (!modalidade.DeduzPensao)
            baseIrrf += varias ? "; as pensões não são deduzidas no desconto simplificado" : "; a pensão não é deduzida no desconto simplificado";

        var formulas = new List<FormulaDto>
        {
            new("Base do IRRF", baseIrrf),
            new($"IR progressivo ({FaixaIrrf(iteracao.Aliquota, iteracao.ImpostoAntesReducao)})", $"{moeda(iteracao.BaseIrrf)} x {percentual(iteracao.Aliquota)} - {moeda(iteracao.Deducao)} = {moeda(iteracao.ImpostoAntesReducao)}"),
            new("IRRF após redução mensal", iteracao.ImpostoAntesReducao - iteracao.ReducaoMensal == iteracao.Imposto
                ? $"{moeda(iteracao.ImpostoAntesReducao)} - {moeda(iteracao.ReducaoMensal)} = {moeda(iteracao.Imposto)}"
                : $"{moeda(iteracao.ImpostoAntesReducao)} - {moeda(iteracao.ReducaoMensal)} = {moeda(iteracao.ImpostoAntesReducao - iteracao.ReducaoMensal)}, que não passa do limite de retenção e não é descontado (Lei 9.430/1996, art. 67): {moeda(iteracao.Imposto)}")
        };

        var anteriores = 0m;
        foreach (var pensao in pensoes)
        {
            var sufixo = varias ? $" - {pensao.Nome}" : "";
            var descontoAnteriores = entrada.Sucessiva ? anteriores : 0m;
            var termoAnteriores = descontoAnteriores > 0m ? $" - {moeda(descontoAnteriores)} (pensões anteriores)" : "";
            switch (pensao.Regra.Base)
            {
                case BasePensao.RendimentosLiquidos:
                    formulas.Add(new($"Base da pensão{sufixo}", $"{moeda(rendimentos)} - {moeda(valorInss)} (INSS) - {moeda(iteracao.Imposto)} (IRRF){termoAnteriores} = {moeda(pensao.Base)}"));
                    break;
                case BasePensao.RendimentosBrutos:
                    formulas.Add(new($"Base da pensão{sufixo}", descontoAnteriores > 0m
                        ? $"{moeda(rendimentos)} ({rotuloRendimentos} brutos){termoAnteriores} = {moeda(pensao.Base)}"
                        : $"{moeda(pensao.Base)} ({rotuloRendimentos} brutos)"));
                    break;
                case BasePensao.SalarioMinimo:
                    formulas.Add(new($"Base da pensão{sufixo}", $"{moeda(pensao.Base)} (salário mínimo de {entrada.Competencia:MM/yyyy})"));
                    break;
            }
            formulas.Add(pensao.Regra.EhPercentual
                ? new($"Pensão calculada{sufixo}", $"{moeda(pensao.Base)} x {percentual(pensao.Regra.Percentual)} = {moeda(pensao.Pensao)}")
                : new($"Pensão{sufixo}", $"Valor fixo: {moeda(pensao.Pensao)}"));
            anteriores += pensao.Pensao;
        }
        if (varias)
            formulas.Add(new("Total das pensões", $"{string.Join(" + ", pensoes.Select(pensao => moeda(pensao.Pensao)))} = {moeda(iteracao.Pensao)}"));
        return formulas;
    }
}
