using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Previdencia;

/// <summary>Regras comuns de valor dos benefícios do RGPS depois da EC 103/2019.</summary>
public static class RegrasBeneficioRgps
{
    /// <summary>Data de publicação e vigência da EC 103/2019.</summary>
    public static readonly DateOnly Reforma = new(2019, 11, 13);

    /// <summary>
    /// Coeficiente da média (EC 103/2019, art. 26, §§ 2º e 5º): 60% mais 2 pontos percentuais por ano completo de
    /// contribuição acima de 20 anos, para o homem, ou de 15 anos, para a mulher.
    /// </summary>
    public static decimal Coeficiente(SexoSegurado sexo, int mesesContribuicao) =>
        60m + 2m * Math.Max(0, mesesContribuicao / 12 - (sexo == SexoSegurado.Feminino ? 15 : 20));

    /// <summary>O benefício que substitui a renda não fica abaixo do salário mínimo nem acima do teto (CF, art. 201, § 2º).</summary>
    public static decimal Limitar(decimal valor, decimal salarioMinimo, decimal teto) =>
        Math.Min(teto, Math.Max(salarioMinimo, CalculadoraTributacao.Arredondar(valor)));

    /// <summary>Meses completos entre duas datas, como a idade em meses.</summary>
    public static int MesesCompletos(DateOnly inicio, DateOnly fim) =>
        (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month - (fim.Day < inicio.Day ? 1 : 0);
}
