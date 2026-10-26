using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Previdencia.PensaoMorte;

/// <summary>
/// Pensão por morte do RGPS para óbitos desde a EC 103/2019 (art. 23): cota familiar de 50% da aposentadoria do segurado,
/// ou da aposentadoria por incapacidade permanente a que teria direito, mais 10 pontos por dependente, até 100%; com
/// dependente inválido ou com deficiência, 100%. A pensão não fica abaixo do salário mínimo e é dividida em partes iguais.
/// A cota do cônjuge dura 4 meses sem 18 contribuições ou 2 anos de união, salvo óbito por acidente; nos demais casos,
/// pela idade do cônjuge (Lei 8.213/1991, art. 77, § 2º, V, com as idades da Portaria ME 424/2020 desde 2021). Acumulada
/// com benefício próprio, o mais vantajoso é pago inteiro e o outro com o redutor por faixas (EC 103/2019, art. 24, § 2º).
/// </summary>
public static class CalculadoraPensaoMorte
{
    private static readonly DateOnly IdadesDaPortaria424 = new(2021, 1, 1);
    private const decimal ValorMaximo = 10_000_000m;

    public static Result<ApuracaoPensaoMorte> Calcular(EntradaPensaoMorte e)
    {
        if (!Enum.IsDefined(e.Situacao) || !Enum.IsDefined(e.Causa) || !Enum.IsDefined(e.SexoInstituidor))
            return Erro.Validacao("Revise a situação do segurado, o sexo e a causa do óbito.");
        if (e.DataObito < RegrasBeneficioRgps.Reforma)
            return Erro.Validacao("A pensão segue as regras da EC 103/2019: informe um óbito a partir de 13/11/2019. Antes, a pensão era de 100% da aposentadoria.");
        if (e.SalarioMinimo <= 0m || e.Teto < e.SalarioMinimo)
            return Erro.Validacao("Faltam o salário mínimo e o teto do INSS da data do óbito.");
        if (e.MesesContribuicao is < 0 or > 900)
            return Erro.Validacao("Revise o tempo de contribuição do segurado.");
        var dependentes = (e.HaConjuge ? 1 : 0) + e.OutrosDependentes;
        if (e.OutrosDependentes is < 0 or > 20 || dependentes == 0)
            return Erro.Validacao("Informe o cônjuge ou ao menos um dependente; até 20 outros dependentes.");
        if (e.HaConjuge && e.IdadeConjuge is < 12 or > 120)
            return Erro.Validacao("Revise a idade do cônjuge na data do óbito.");
        foreach (var (valor, nome) in new[] { (e.ValorAposentadoria, "a aposentadoria do segurado"), (e.Media, "a média dos salários"), (e.BeneficioConjuge, "o benefício próprio do cônjuge") })
            if (valor < 0m || valor > ValorMaximo || decimal.Round(valor, 2) != valor)
                return Erro.Validacao($"Revise {nome}: informe um valor em reais e centavos.");

        decimal baseCalculo;
        decimal? coeficiente = null;
        if (e.Situacao == SituacaoInstituidor.Aposentado)
        {
            if (e.ValorAposentadoria <= 0m)
                return Erro.Validacao("Informe a aposentadoria recebida pelo segurado, o valor mensal bruto.");
            baseCalculo = e.ValorAposentadoria;
        }
        else
        {
            if (e.Media <= 0m)
                return Erro.Validacao("Informe a média dos salários de contribuição do segurado.");
            // Aposentadoria por incapacidade permanente: 100% da média se decorre de acidente do trabalho ou doença profissional.
            coeficiente = e.Causa == CausaObito.AcidenteTrabalho ? 100m : RegrasBeneficioRgps.Coeficiente(e.SexoInstituidor, e.MesesContribuicao);
            baseCalculo = RegrasBeneficioRgps.Limitar(e.Media * coeficiente.Value / 100m, e.SalarioMinimo, e.Teto);
        }

        var cota = e.DependenteInvalido ? 100m : Math.Min(100m, 50m + 10m * dependentes);
        var calculada = CalculadoraTributacao.Arredondar(baseCalculo * cota / 100m);
        var pensao = Math.Max(e.SalarioMinimo, calculada);
        var porDependente = CalculadoraTributacao.Arredondar(pensao / dependentes);

        int? duracao = null;
        var vitalicia = false;
        if (e.HaConjuge)
        {
            var dispensa = e.Causa != CausaObito.Comum;
            if (!dispensa && (e.MesesContribuicao < 18 || !e.UniaoDoisAnos))
                duracao = 4;
            else if (DuracaoPorIdade(e.IdadeConjuge, e.DataObito) is { } anos)
                duracao = anos * 12;
            else
                vitalicia = true;
        }

        decimal? reduzido = null;
        var reduzidoPensao = false;
        if (e.HaConjuge && e.BeneficioConjuge > 0m)
        {
            reduzidoPensao = porDependente <= e.BeneficioConjuge;
            reduzido = Redutor(reduzidoPensao ? porDependente : e.BeneficioConjuge, e.SalarioMinimo);
        }

        return new ApuracaoPensaoMorte(e, baseCalculo, coeficiente, dependentes, cota, calculada, pensao, porDependente, duracao, vitalicia, reduzidoPensao, reduzido);
    }

    /// <summary>
    /// Anos de pensão do cônjuge pela idade no óbito; nulo quando vitalícia. Para óbitos desde 01/01/2021 (Portaria ME
    /// 424/2020, reproduzida na IN PRES/INSS 128/2022, art. 375, § 8º): menos de 22 anos, 3; de 22 a 27, 6; de 28 a 30, 10;
    /// de 31 a 41, 15; de 42 a 44, 20; 45 ou mais, vitalícia. Antes, as idades da Lei 8.213/1991, art. 77, § 2º, V, "c",
    /// eram um ano menores: menos de 21, de 21 a 26, de 27 a 29, de 30 a 40, de 41 a 43 e 44 ou mais.
    /// </summary>
    public static int? DuracaoPorIdade(int idade, DateOnly obito)
    {
        var ajuste = obito >= IdadesDaPortaria424 ? 0 : 1;
        return (idade + ajuste) switch
        {
            < 22 => 3,
            < 28 => 6,
            < 31 => 10,
            < 42 => 15,
            < 45 => 20,
            _ => null
        };
    }

    /// <summary>
    /// Redutor do benefício menos vantajoso na acumulação (EC 103/2019, art. 24, § 2º): inteiro até 1 salário mínimo, 60% da
    /// parte entre 1 e 2, 40% entre 2 e 3, 20% entre 3 e 4 e 10% acima de 4 salários mínimos.
    /// </summary>
    public static decimal Redutor(decimal valor, decimal salarioMinimo)
    {
        decimal Faixa(int de, int ate) => Math.Clamp(valor - de * salarioMinimo, 0m, (ate - de) * salarioMinimo);
        var parte = Math.Min(valor, salarioMinimo) + Faixa(1, 2) * 0.6m + Faixa(2, 3) * 0.4m + Faixa(3, 4) * 0.2m + Math.Max(0m, valor - 4 * salarioMinimo) * 0.1m;
        return CalculadoraTributacao.Arredondar(parte);
    }
}
