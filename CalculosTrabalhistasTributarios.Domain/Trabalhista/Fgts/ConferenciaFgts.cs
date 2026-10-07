using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;

/// <summary>
/// Conferência local dos depósitos do FGTS: o devido de cada competência (remuneração × alíquota) contra o depósito
/// informado, com o vencimento de cada um. Até a competência 02/2024, o depósito mensal vencia no dia 7 do mês seguinte;
/// a partir de 03/2024, com o FGTS Digital, vence no dia 20 (Lei 8.036/1990, art. 15, na redação da Lei 14.438/2022).
/// Sem expediente bancário, o prazo é antecipado; aqui, só sábados e domingos são considerados. O rescisório vence no
/// 10º dia corrido após o desligamento. Juros, multa e atualização de depósitos em atraso (art. 22) não são calculados.
/// </summary>
public static class ConferenciaFgts
{
    public const int LimiteLinhas = 720;
    private const decimal LimiteValor = 1_000_000_000m;
    private const decimal AliquotaCompensatoria = 3.2m;
    private static readonly DateOnly InicioFgtsDigital = new(2024, 3, 1);

    public static decimal Aliquota(CategoriaFgts categoria) => categoria == CategoriaFgts.Aprendiz ? 2m : 8m;

    public static Result<ApuracaoConferenciaFgts> Calcular(CategoriaFgts categoria, IReadOnlyList<LancamentoFgts> lancamentos, DateOnly? desligamento)
    {
        if (!Enum.IsDefined(categoria))
            return Erro.Validacao("Escolha a categoria do trabalhador: empregado, aprendiz ou doméstico.");
        if (lancamentos is null || lancamentos.Count is 0 or > LimiteLinhas)
            return Erro.Validacao($"Informe de 1 a {LimiteLinhas} competências para conferir.");

        var mesDesligamento = desligamento is { } data ? new DateOnly(data.Year, data.Month, 1) : (DateOnly?)null;
        var vistas = new HashSet<(DateOnly, TipoCompetenciaFgts)>();
        foreach (var item in lancamentos)
        {
            if (item is null || !Enum.IsDefined(item.Tipo))
                return Erro.Validacao("Revise o tipo das competências: mensal ou rescisória.");
            var referencia = $"{item.Competencia:MM/yyyy}";
            if (!Valido(item.Remuneracao) || !Valido(item.DepositoInformado))
                return Erro.Validacao($"Revise {referencia}: remuneração e depósito devem ser positivos ou zero, até R$ 1 bilhão, com no máximo dois decimais.");
            if (!vistas.Add((item.Competencia, item.Tipo)))
                return Erro.Validacao($"A competência {referencia} {(item.Tipo == TipoCompetenciaFgts.Mensal ? "mensal" : "rescisória")} aparece mais de uma vez: some os valores em uma linha.");
            if (item.Tipo == TipoCompetenciaFgts.Rescisoria && mesDesligamento is null)
                return Erro.Validacao($"A competência rescisória {referencia} exige a data de desligamento.");
            if (item.Tipo == TipoCompetenciaFgts.Rescisoria && item.Competencia != mesDesligamento)
                return Erro.Validacao($"A competência rescisória deve ser a do desligamento ({mesDesligamento:MM/yyyy}); {referencia} é mensal.");
            if (item.Tipo == TipoCompetenciaFgts.Mensal && mesDesligamento is { } mes && item.Competencia >= mes)
                return Erro.Validacao($"A competência {referencia} é do desligamento ou posterior: lance-a como rescisória.");
        }

        var aliquota = Aliquota(categoria);
        var linhas = lancamentos.OrderBy(item => item.Competencia).ThenBy(item => item.Tipo).Select(item =>
        {
            var devido = CalculadoraTributacao.Arredondar(item.Remuneracao * aliquota / 100m);
            var compensatoria = categoria == CategoriaFgts.Domestico ? CalculadoraTributacao.Arredondar(item.Remuneracao * AliquotaCompensatoria / 100m) : 0m;
            return new LinhaConferenciaFgts(item, devido, compensatoria, devido - item.DepositoInformado, Vencimento(item, desligamento));
        }).ToArray();
        return new ApuracaoConferenciaFgts(categoria, aliquota, linhas, desligamento);
    }

    private static DateOnly? Vencimento(LancamentoFgts item, DateOnly? desligamento)
    {
        if (item.Tipo == TipoCompetenciaFgts.Rescisoria)
            return desligamento?.AddDays(10);
        var seguinte = item.Competencia.AddMonths(1);
        var dia = new DateOnly(seguinte.Year, seguinte.Month, item.Competencia >= InicioFgtsDigital ? 20 : 7);
        while (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            dia = dia.AddDays(-1);
        return dia;
    }

    private static bool Valido(decimal valor) => valor >= 0m && valor <= LimiteValor && decimal.Round(valor, 2) == valor;
}
