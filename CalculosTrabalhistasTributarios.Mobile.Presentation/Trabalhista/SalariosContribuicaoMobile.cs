using System.Collections.Specialized;
using System.Globalization;
using CalculosTrabalhistasTributarios.Domain.Previdencia;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>
/// Origem da média e grade de salários de contribuição desde 07/1994 da aposentadoria e da pensão por morte, como em
/// <c>CamposMediaSalarios</c> e <c>CampoSalariosContribuicaoViewModel</c> do desktop: cada linha junta as competências
/// seguidas com o mesmo salário, e o novo período começa no mês seguinte ao fim do anterior.
/// </summary>
internal static class SalariosContribuicaoMobile
{
    public const string ChaveOrigem = "origemMedia";
    public const string ChaveMedia = "mediaInformada";
    public const string ChaveGrade = "salarios";
    public const string Titulo = "Salários de contribuição";
    public const string NomeLinha = "Período";
    public static readonly IReadOnlyList<string> OpcoesOrigem = ["Calcular pelos salários", "Informar a média pronta"];
    public const string DicaMedia = "Média de todos os salários de contribuição desde 07/1994, corrigidos, como no simulador do Meu INSS.";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static IReadOnlyList<ColunaGradeMobile> Colunas { get; } =
    [
        new("inicio", "De (MM/AAAA)", TipoCampoMobile.Competencia, Dica: "Primeira competência do período, desde 07/1994."),
        new("fim", "Até (MM/AAAA)", TipoCampoMobile.Competencia, Dica: "Última competência; para um só mês, repita a de início."),
        new("salario", "Salário (R$)", TipoCampoMobile.Moeda, "0,00", "Salário de contribuição do mês, como no CNIS; some os vínculos do mesmo mês.")
    ];

    /// <param name="referencia">Até quando os salários são corrigidos, como "a data da simulação" ou "o óbito".</param>
    public static string Descricao(string referencia) =>
        "Uma linha para cada sequência de meses com o mesmo salário, ou um mês por linha. Some os vínculos do mesmo mês numa só linha. "
        + $"Cada mês é corrigido pelo INPC até {referencia}, e a média é a soma dos salários corrigidos dividida pela quantidade de meses. "
        + $"Até {CalculadoraMediaSalarios.LimitePeriodos} períodos.";

    /// <summary>Preenche o início do período novo: 07/1994 no primeiro e o mês seguinte ao fim do anterior nos demais.</summary>
    public static void PreencherInicioDosNovos(GradeMobile grade) =>
        grade.Linhas.CollectionChanged += (_, args) =>
        {
            if (args.Action != NotifyCollectionChangedAction.Add || args.NewItems is null) return;
            foreach (LinhaGradeMobile linha in args.NewItems)
            {
                var inicio = linha.Campo("inicio");
                if (inicio.Valor.Trim().Length > 0) continue;
                var indice = grade.Linhas.IndexOf(linha);
                if (indice == 0)
                    inicio.Valor = CalculadoraMediaSalarios.InicioDaMedia.ToString("MM/yyyy", Cultura);
                else if (indice > 0 && DateOnly.TryParseExact("01/" + grade.Linhas[indice - 1].Campo("fim").Valor.Trim(), "dd/MM/yyyy",
                    Cultura, DateTimeStyles.None, out var fim))
                    inicio.Valor = fim.AddMonths(1).ToString("MM/yyyy", Cultura);
            }
        };

    public static IReadOnlyList<PeriodoSalarioContribuicao> Ler(GradeMobile grade)
    {
        if (grade.Linhas.Count == 0)
            throw new EntradaInvalidaException("Adicione os períodos de salário de contribuição desde 07/1994, ou escolha informar a média pronta.");
        return grade.Ler(linha =>
        {
            DateOnly inicio, fim;
            try
            {
                inicio = LeituraCampoMobile.Competencia(linha.Campo("inicio"));
                fim = LeituraCampoMobile.Competencia(linha.Campo("fim"));
            }
            catch (EntradaInvalidaException)
            {
                throw new EntradaInvalidaException("informe o início e o fim no formato MM/AAAA; para um só mês, repita a competência.");
            }
            return new PeriodoSalarioContribuicao(inicio, fim, LeituraCampoMobile.Numero(linha.Campo("salario")));
        });
    }
}
