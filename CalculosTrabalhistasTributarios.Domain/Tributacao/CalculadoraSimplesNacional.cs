using CalculosTrabalhistasTributarios.Domain.Comum;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// DAS de um período de apuração do Simples Nacional para uma atividade, com a receita (RBT12) e a folha (FS12) de 12
/// meses e o fator r (Resolução CGSN 140/2018, arts. 21, 22 e 26):
/// <list type="bullet">
/// <item>até 12/2026, a janela são os 12 meses anteriores ao período; no 1º mês de atividade, a receita do mês × 12, e
/// nos 11 meses seguintes a média dos meses anteriores × 12;</item>
/// <item>a partir de 01/2027 (Resolução CGSN 190/2026), a janela são os 12 meses antecedentes ao mês anterior ao do
/// período; no 1º e no 2º mês de atividade valem a 1ª faixa e o fator r de 0,28, e do 3º ao 13º mês a média dos meses
/// antecedentes ao mês anterior × 12.</item>
/// </list>
/// A folha com encargos inclui salários e pró-labore pagos, a CPP e o FGTS recolhidos, sem aluguéis nem lucros (art. 26,
/// §§ 1º a 3º). Valores de exportação, retenção de ISS, substituição tributária e atividades em mais de um anexo ficam fora.
/// </summary>
public static class CalculadoraSimplesNacional
{
    private const decimal LimiteValor = 1_000_000_000m;
    private const decimal AliquotaCppAnexoIV = 20m;
    private static readonly DateOnly InicioRegra2027 = new(2027, 1, 1);

    /// <summary>Competências que o período de apuração exige, conforme a regra vigente e o início de atividade.</summary>
    public static JanelaSimples Janela(DateOnly periodo, DateOnly? inicio)
    {
        var regra2027 = periodo >= InicioRegra2027;
        // Até 2026: 12 meses anteriores ao período. A partir de 2027: 12 meses antecedentes ao mês anterior.
        var ultimo = periodo.AddMonths(regra2027 ? -2 : -1);
        var primeiro = ultimo.AddMonths(-11);
        if (inicio is not { } mesInicio)
            return new JanelaSimples(Meses(primeiro, ultimo), RegraReceitaSimples.DozeMeses, null, regra2027);

        var mes = (periodo.Year - mesInicio.Year) * 12 + periodo.Month - mesInicio.Month + 1;
        if (regra2027 && mes <= 2)
            return new JanelaSimples([], RegraReceitaSimples.PrimeiraFaixa, mes, true);
        if (!regra2027 && mes == 1)
            return new JanelaSimples([], RegraReceitaSimples.PrimeiroMes, mes, false);
        // Até o 12º mês (13º a partir de 2027), a janela começa no início de atividade e a receita é a média × 12.
        var emInicio = mesInicio > primeiro;
        return new JanelaSimples(Meses(emInicio ? mesInicio : primeiro, ultimo), emInicio ? RegraReceitaSimples.MediaInicio : RegraReceitaSimples.DozeMeses, mes, regra2027);
    }

    public static Result<ApuracaoSimples> Calcular(EntradaSimples e)
    {
        if (!Enum.IsDefined(e.Atividade))
            return Erro.Validacao("Escolha a atividade da empresa.");
        if (!SimplesNacional.TemTabela(e.PeriodoApuracao))
            return Erro.NaoEncontrado($"Não há tabela do Simples Nacional para {e.PeriodoApuracao:MM/yyyy}: o cálculo cobre de 01/2018 a 12/2028.");
        if (e.InicioAtividade > e.PeriodoApuracao)
            return Erro.Validacao("O início das atividades não pode ser posterior ao período de apuração.");
        if (!Valido(e.ReceitaMes) || !Valido(e.FolhaMes) || !Valido(e.RemuneracoesMes))
            return Erro.Validacao("A receita, a folha e as remunerações do mês devem ser positivas ou zero, até R$ 1 bilhão, com no máximo dois decimais.");

        var janela = Janela(e.PeriodoApuracao, e.InicioAtividade);
        var porMes = new Dictionary<DateOnly, MesSimples>();
        foreach (var mes in e.Meses ?? [])
        {
            if (mes is null || !Valido(mes.Receita) || !Valido(mes.Folha))
                return Erro.Validacao($"Revise os valores de {mes?.Competencia:MM/yyyy}: receita e folha devem ser positivas ou zero, até R$ 1 bilhão, com dois decimais.");
            if (!porMes.TryAdd(mes.Competencia, mes))
                return Erro.Validacao($"A competência {mes.Competencia:MM/yyyy} aparece mais de uma vez.");
        }
        var faltantes = janela.Meses.Where(mes => !porMes.ContainsKey(mes)).ToArray();
        if (faltantes.Length > 0)
            return Erro.Validacao($"Informe a receita e a folha de {Lista(faltantes)}. Use Gerar meses para criar as competências que o período exige.");
        var considerados = janela.Meses.Select(mes => porMes[mes]).ToArray();

        var (rbt12, fs12) = janela.Regra switch
        {
            RegraReceitaSimples.PrimeiroMes => (e.ReceitaMes * 12m, e.FolhaMes * 12m),
            RegraReceitaSimples.MediaInicio => (Anualizar(considerados.Sum(item => item.Receita), considerados.Length), Anualizar(considerados.Sum(item => item.Folha), considerados.Length)),
            RegraReceitaSimples.PrimeiraFaixa => (0m, 0m),
            _ => (considerados.Sum(item => item.Receita), considerados.Sum(item => item.Folha))
        };
        if (rbt12 > SimplesNacional.LimiteReceitaAnual)
            return Erro.Validacao($"A receita de 12 meses (RBT12) de R$ {rbt12.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"))} passa do limite do Simples Nacional, de R$ 4.800.000,00. A empresa fica sujeita à exclusão; confira o enquadramento com o contador.");

        decimal? fatorR = null;
        string? caso = null;
        var anexo = e.Atividade switch
        {
            AtividadeSimples.Comercio => AnexoSimples.I,
            AtividadeSimples.Industria => AnexoSimples.II,
            AtividadeSimples.ServicosAnexoIII => AnexoSimples.III,
            AtividadeSimples.ServicosAnexoIV => AnexoSimples.IV,
            _ => AnexoSimples.V
        };
        if (e.Atividade == AtividadeSimples.ServicosFatorR)
        {
            (fatorR, caso) = FatorR(janela.Regra, e, rbt12, fs12);
            anexo = fatorR >= SimplesNacional.FatorRMinimo / 100m ? AnexoSimples.III : AnexoSimples.V;
        }

        AliquotaSimples Aliquota(AnexoSimples item) => janela.Regra == RegraReceitaSimples.PrimeiraFaixa
            ? SimplesNacional.PrimeiraFaixa(item)
            : SimplesNacional.Aliquota(item, rbt12, e.PeriodoApuracao).Valor;
        var aliquota = Aliquota(anexo);
        var comparar = e.Atividade == AtividadeSimples.ServicosFatorR;
        var das = CalculadoraTributacao.Arredondar(e.ReceitaMes * aliquota.AliquotaEfetiva / 100m);
        var cpp = anexo == AnexoSimples.IV ? CalculadoraTributacao.Arredondar(e.RemuneracoesMes * AliquotaCppAnexoIV / 100m) : 0m;
        return new ApuracaoSimples(janela, considerados, e.ReceitaMes, rbt12, fs12, fatorR, caso, aliquota, das,
            comparar ? Aliquota(AnexoSimples.III) : null, comparar ? Aliquota(AnexoSimples.V) : null, cpp);
    }

    /// <summary>Fator r com os valores fixados pela resolução quando a folha ou a receita é zero (art. 26, §§ 6º e 7º).</summary>
    private static (decimal Fator, string? Caso) FatorR(RegraReceitaSimples regra, EntradaSimples e, decimal rbt12, decimal fs12)
    {
        if (regra == RegraReceitaSimples.PrimeiraFaixa)
            return (0.28m, "1º ou 2º mês de atividade: fator r de 0,28 (art. 26, § 6º).");
        var (folha, receita, origem) = regra == RegraReceitaSimples.PrimeiroMes
            ? (e.FolhaMes, e.ReceitaMes, "do próprio mês de início")
            : (fs12, rbt12, "dos 12 meses");
        return (folha, receita) switch
        {
            ( > 0m, > 0m) => (folha / receita, null),
            ( > 0m, _) => (0.28m, $"Há folha e não há receita {origem}: fator r de 0,28 (art. 26, §§ 6º e 7º)."),
            _ => (0.01m, $"Não há folha {origem}: fator r de 0,01 (art. 26, §§ 6º e 7º).")
        };
    }

    private static decimal Anualizar(decimal soma, int meses) => CalculadoraTributacao.Arredondar(soma / meses * 12m);

    private static DateOnly[] Meses(DateOnly primeiro, DateOnly ultimo)
    {
        var quantidade = (ultimo.Year - primeiro.Year) * 12 + ultimo.Month - primeiro.Month + 1;
        return Enumerable.Range(0, Math.Max(0, quantidade)).Select(primeiro.AddMonths).ToArray();
    }

    private static string Lista(IReadOnlyList<DateOnly> meses) =>
        meses.Count <= 4 ? string.Join(", ", meses.Select(mes => mes.ToString("MM/yyyy"))) : $"{meses.Count} competências, de {meses[0]:MM/yyyy} a {meses[^1]:MM/yyyy}";

    private static bool Valido(decimal valor) => valor >= 0m && valor <= LimiteValor && decimal.Round(valor, 2) == valor;
}
