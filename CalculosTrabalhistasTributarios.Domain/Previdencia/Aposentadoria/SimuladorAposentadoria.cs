using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Previdencia.Aposentadoria;

/// <summary>
/// Projeta, mês a mês a partir da data da simulação, quando o segurado cumpre cada regra de aposentadoria do RGPS da
/// EC 103/2019 e estima o benefício sobre a média informada, em valores de hoje:
/// <list type="bullet">
/// <item>Pontos (art. 15): 35/30 anos de contribuição e idade + tempo de 96/86 pontos em 2019, mais 1 por ano até 105/100.</item>
/// <item>Idade mínima progressiva (art. 16): 35/30 anos e idade de 61/56 anos em 2019, mais 6 meses por ano até 65/62.</item>
/// <item>Pedágio de 50% (art. 17): para quem tinha mais de 33/28 anos em 13/11/2019; o tempo que faltava mais metade; fator previdenciário.</item>
/// <item>Pedágio de 100% (art. 20): 60/57 anos de idade e o tempo que faltava em dobro; 100% da média.</item>
/// <item>Transição por idade (art. 18): 65 anos (homem) ou 60 anos em 2019 mais 6 meses por ano até 62 (mulher), com 15 anos.</item>
/// <item>Permanente (art. 19): para quem se filiou depois da reforma, 65/62 anos com 20/15 anos de contribuição.</item>
/// </list>
/// Nas regras de pontos, idade e permanente, o benefício é 60% da média mais 2 pontos por ano acima de 20 (homem) ou 15
/// anos (mulher), entre o salário mínimo e o teto (art. 26). A projeção é mensal e não considera carência, tempo especial,
/// professor nem a revisão da média.
/// </summary>
public static class SimuladorAposentadoria
{
    public const int HorizonteMeses = 600;
    private const decimal Aliquota = 0.31m;

    public static Result<ApuracaoAposentadoria> Simular(EntradaAposentadoria e)
    {
        if (!Enum.IsDefined(e.Sexo))
            return Erro.Validacao("Informe o sexo do segurado.");
        var idade = RegrasBeneficioRgps.MesesCompletos(e.Nascimento, e.DataSimulacao);
        if (idade < 14 * 12 || idade > 100 * 12)
            return Erro.Validacao("Revise a data de nascimento: o segurado deve ter de 14 a 100 anos na data da simulação.");
        if (e.MesesContribuicao < 0 || e.MesesNaReforma < 0 || e.MesesNaReforma > e.MesesContribuicao)
            return Erro.Validacao("O tempo de contribuição em 13/11/2019 não pode ser negativo nem maior que o tempo até hoje.");
        if (e.MesesContribuicao > idade - 12 * 12)
            return Erro.Validacao("O tempo de contribuição passa da idade do segurado; revise os anos e os meses informados.");
        if (e.DataSimulacao < RegrasBeneficioRgps.Reforma)
            return Erro.Validacao("A simulação usa as regras da EC 103/2019: informe uma data de simulação a partir de 13/11/2019.");
        var anosNaReforma = e.Nascimento.AddYears(12) > RegrasBeneficioRgps.Reforma ? 0 : RegrasBeneficioRgps.MesesCompletos(e.Nascimento.AddYears(12), RegrasBeneficioRgps.Reforma);
        if (e.MesesNaReforma > anosNaReforma)
            return Erro.Validacao("O tempo de contribuição em 13/11/2019 passa da idade do segurado naquela data; revise-o.");
        if (e.Media <= 0m || e.Media > 10_000_000m || decimal.Round(e.Media, 2) != e.Media)
            return Erro.Validacao("Informe a média dos salários de contribuição em reais e centavos, maior que zero.");
        if (e.SalarioMinimo <= 0m || e.Teto < e.SalarioMinimo)
            return Erro.Validacao("Faltam o salário mínimo e o teto do INSS da data da simulação.");
        if (e.ExpectativaSobrevida is { } expectativa && (expectativa <= 0m || expectativa > 80m))
            return Erro.Validacao("A expectativa de sobrevida deve ficar entre 0 e 80 anos, conforme a tábua do IBGE.");

        var mulher = e.Sexo == SexoSegurado.Feminino;
        var antes = e.MesesNaReforma > 0;
        var tempoIntegral = mulher ? 30 * 12 : 35 * 12;
        var falta = Math.Max(0, tempoIntegral - e.MesesNaReforma);
        var regras = new List<ResultadoRegraAposentadoria>
        {
            Avaliar(e, RegraAposentadoria.Pontos, mulher ? "30 anos de contribuição e 86 pontos (idade + tempo) em 2019, mais 1 por ano até 100" : "35 anos de contribuição e 96 pontos (idade + tempo) em 2019, mais 1 por ano até 105",
                antes ? null : "Só para quem contribuía antes de 13/11/2019.",
                (data, idadeMeses, tempo) => tempo >= tempoIntegral && idadeMeses + tempo >= 12 * Math.Min(mulher ? 100 : 105, (mulher ? 86 : 96) + Math.Max(0, data.Year - 2019))),
            Avaliar(e, RegraAposentadoria.IdadeProgressiva, mulher ? "30 anos de contribuição e 56 anos de idade em 2019, mais 6 meses por ano até 62" : "35 anos de contribuição e 61 anos de idade em 2019, mais 6 meses por ano até 65",
                antes ? null : "Só para quem contribuía antes de 13/11/2019.",
                (data, idadeMeses, tempo) => tempo >= tempoIntegral && idadeMeses >= Math.Min(mulher ? 62 * 12 : 65 * 12, (mulher ? 56 * 12 : 61 * 12) + 6 * Math.Max(0, data.Year - 2019))),
            Avaliar(e, RegraAposentadoria.Pedagio50, $"{tempoIntegral / 12} anos de contribuição mais metade do tempo que faltava em 13/11/2019 ({Meses((falta + 1) / 2)}), sem idade mínima",
                e.MesesNaReforma > (mulher ? 28 * 12 : 33 * 12) ? null : $"Só para quem tinha mais de {(mulher ? 28 : 33)} anos de contribuição em 13/11/2019.",
                (_, _, tempo) => tempo >= tempoIntegral + (falta + 1) / 2),
            Avaliar(e, RegraAposentadoria.Pedagio100, $"{(mulher ? 57 : 60)} anos de idade e {tempoIntegral / 12} anos de contribuição mais o tempo que faltava em 13/11/2019 ({Meses(falta)})",
                antes ? null : "Só para quem contribuía antes de 13/11/2019.",
                (_, idadeMeses, tempo) => idadeMeses >= (mulher ? 57 : 60) * 12 && tempo >= tempoIntegral + falta),
            Avaliar(e, RegraAposentadoria.IdadeTransicao, mulher ? "15 anos de contribuição e 60 anos de idade em 2019, mais 6 meses por ano até 62" : "15 anos de contribuição e 65 anos de idade",
                antes ? null : "Só para quem contribuía antes de 13/11/2019; depois, vale a regra permanente.",
                (data, idadeMeses, tempo) => tempo >= 15 * 12 && idadeMeses >= (mulher ? Math.Min(62 * 12, 60 * 12 + 6 * Math.Max(0, data.Year - 2019)) : 65 * 12)),
            Avaliar(e, RegraAposentadoria.Permanente, mulher ? "62 anos de idade e 15 anos de contribuição" : "65 anos de idade e 20 anos de contribuição",
                antes ? "Para quem se filiou depois de 13/11/2019; antes, vale a transição por idade, com 15 anos de contribuição." : null,
                (_, idadeMeses, tempo) => idadeMeses >= (mulher ? 62 : 65) * 12 && tempo >= (mulher ? 15 : 20) * 12)
        };
        return new ApuracaoAposentadoria(e, idade, regras);
    }

    /// <summary>
    /// Fator previdenciário (Lei 8.213/1991, art. 29, §§ 7º e 9º): (Tc × a ÷ Es) × [1 + (Id + Tc × a) ÷ 100], com a = 0,31
    /// e 5 anos a mais de tempo para a mulher, arredondado em 4 casas.
    /// </summary>
    public static decimal FatorPrevidenciario(SexoSegurado sexo, int idadeMeses, int tempoMeses, decimal expectativa)
    {
        var tempo = tempoMeses / 12m + (sexo == SexoSegurado.Feminino ? 5m : 0m);
        var idade = idadeMeses / 12m;
        return decimal.Round(tempo * Aliquota / expectativa * (1m + (idade + tempo * Aliquota) / 100m), 4, MidpointRounding.AwayFromZero);
    }

    private static ResultadoRegraAposentadoria Avaliar(EntradaAposentadoria e, RegraAposentadoria regra, string requisitos, string? motivo, Func<DateOnly, int, int, bool> cumpre)
    {
        if (motivo is not null)
            return new ResultadoRegraAposentadoria(regra, requisitos, motivo);
        for (var mes = 0; mes <= HorizonteMeses; mes++)
        {
            var data = e.DataSimulacao.AddMonths(mes);
            var idade = RegrasBeneficioRgps.MesesCompletos(e.Nascimento, data);
            if (idade > 100 * 12)
                break;
            var tempo = e.MesesContribuicao + (e.ContinuaContribuindo ? mes : 0);
            if (!cumpre(data, idade, tempo))
                continue;
            return regra switch
            {
                RegraAposentadoria.Pedagio100 => new(regra, requisitos, null, data, idade, tempo, 100m, null, RegrasBeneficioRgps.Limitar(e.Media, e.SalarioMinimo, e.Teto)),
                RegraAposentadoria.Pedagio50 when e.ExpectativaSobrevida is { } expectativa => Fator(e, regra, requisitos, data, idade, tempo, expectativa),
                RegraAposentadoria.Pedagio50 => new(regra, requisitos, null, data, idade, tempo),
                _ => Coeficiente(e, regra, requisitos, data, idade, tempo)
            };
        }
        return new ResultadoRegraAposentadoria(regra, requisitos, null);
    }

    private static ResultadoRegraAposentadoria Coeficiente(EntradaAposentadoria e, RegraAposentadoria regra, string requisitos, DateOnly data, int idade, int tempo)
    {
        var coeficiente = RegrasBeneficioRgps.Coeficiente(e.Sexo, tempo);
        return new(regra, requisitos, null, data, idade, tempo, coeficiente, null, RegrasBeneficioRgps.Limitar(e.Media * coeficiente / 100m, e.SalarioMinimo, e.Teto));
    }

    private static ResultadoRegraAposentadoria Fator(EntradaAposentadoria e, RegraAposentadoria regra, string requisitos, DateOnly data, int idade, int tempo, decimal expectativa)
    {
        var fator = FatorPrevidenciario(e.Sexo, idade, tempo, expectativa);
        return new(regra, requisitos, null, data, idade, tempo, null, fator, RegrasBeneficioRgps.Limitar(e.Media * fator, e.SalarioMinimo, e.Teto));
    }

    private static string Meses(int meses) => $"{meses / 12} anos e {meses % 12} meses";
}
