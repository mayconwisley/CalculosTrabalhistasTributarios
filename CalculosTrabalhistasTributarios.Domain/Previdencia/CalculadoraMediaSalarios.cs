using CalculosTrabalhistasTributarios.Domain.Atualizacao;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Previdencia;

/// <summary>
/// Média dos salários de contribuição do RGPS (EC 103/2019, art. 26): média aritmética simples de todos os salários de
/// contribuição desde a competência 07/1994, ou desde o início das contribuições, se posterior. Cada salário é corrigido
/// mês a mês pela variação integral do INPC (Lei 8.213/1991, art. 29-B): o fator da competência é o produto de (1 + INPC)
/// dela até o mês anterior ao da simulação, que faz as vezes do início do benefício. Cada salário corrigido é arredondado
/// aos centavos, e a média, à soma deles dividida pela quantidade de competências. Os salários entram como informados, já
/// limitados ao teto da época, como constam no CNIS; a exclusão das contribuições que reduzem a média (art. 26, § 6º) não é
/// aplicada.
/// </summary>
public static class CalculadoraMediaSalarios
{
    public static readonly DateOnly InicioDaMedia = new(1994, 7, 1);
    public const int LimitePeriodos = 240;
    private const decimal ValorMaximo = 10_000_000m;
    private const int MesesSemInpcAceitos = 2;

    public static Result<ApuracaoMediaSalarios> Calcular(IReadOnlyList<PeriodoSalarioContribuicao> periodos, DateOnly dataSimulacao, IReadOnlyDictionary<DateOnly, decimal> inpc)
    {
        if (periodos is null || periodos.Count == 0)
            return Erro.Validacao("Informe os salários de contribuição: ao menos um período, com início, fim e salário.");
        if (periodos.Count > LimitePeriodos)
            return Erro.Validacao($"Informe até {LimitePeriodos} períodos de salário; junte os meses seguidos que têm o mesmo salário.");

        var mesSimulacao = SerieMensal.MesDe(dataSimulacao);
        var salarios = new SortedDictionary<DateOnly, decimal>();
        foreach (var periodo in periodos)
        {
            var inicio = SerieMensal.MesDe(periodo.Inicio);
            var fim = SerieMensal.MesDe(periodo.Fim);
            var nome = $"o período de {inicio:MM/yyyy} a {fim:MM/yyyy}";
            if (fim < inicio)
                return Erro.Validacao($"Revise {nome}: o fim deve ser igual ou posterior ao início.");
            if (inicio < InicioDaMedia)
                return Erro.Validacao($"Revise {nome}: a média usa os salários desde 07/1994 (EC 103/2019, art. 26). Comece o período em 07/1994 ou depois.");
            if (fim > mesSimulacao)
                return Erro.Validacao($"Revise {nome}: o período passa do mês da simulação ({mesSimulacao:MM/yyyy}).");
            if (periodo.Salario <= 0m || periodo.Salario > ValorMaximo || decimal.Round(periodo.Salario, 2) != periodo.Salario)
                return Erro.Validacao($"Revise o salário de {nome}: informe um valor maior que zero, em reais e centavos.");
            for (var mes = inicio; mes <= fim; mes = mes.AddMonths(1))
                if (!salarios.TryAdd(mes, periodo.Salario))
                    return Erro.Validacao($"A competência {mes:MM/yyyy} aparece em mais de um período. Some os salários dos vínculos do mês num só período.");
        }

        // Corrige até o mês anterior ao da simulação. O IBGE publica o INPC no mês seguinte ao de referência: até dois
        // meses finais ainda sem índice ficam sem correção, com aviso; mais que isso indica a tabela desatualizada.
        var primeiro = salarios.Keys.First();
        var limite = mesSimulacao.AddMonths(-1);
        var ultimo = limite;
        while (ultimo >= primeiro && !inpc.ContainsKey(ultimo))
            ultimo = ultimo.AddMonths(-1);
        DateOnly? faltaDesde = ultimo < limite && limite >= primeiro ? (ultimo < primeiro ? primeiro : ultimo.AddMonths(1)) : null;
        if (faltaDesde is { } desde && SerieMensal.Quantidade(desde, limite) > MesesSemInpcAceitos)
            return Erro.Validacao($"Faltam os índices do INPC de {desde:MM/yyyy} a {limite:MM/yyyy}: atualize a tabela INPC pela internet para corrigir os salários.");

        var fatores = new Dictionary<DateOnly, decimal>();
        var fator = 1m;
        for (var mes = ultimo; mes >= primeiro; mes = mes.AddMonths(-1))
        {
            if (!inpc.TryGetValue(mes, out var variacao))
                return Erro.Validacao($"Falta o INPC de {mes:MM/yyyy} na tabela de índices: atualize a tabela INPC para corrigir os salários.");
            fator *= 1m + variacao / 100m;
            fatores[mes] = fator;
        }

        var meses = salarios.Select(item =>
        {
            var aplicado = fatores.TryGetValue(item.Key, out var valor) ? valor : 1m;
            return new SalarioCorrigido(item.Key, item.Value, aplicado, CalculadoraTributacao.Arredondar(item.Value * aplicado));
        }).ToArray();
        var soma = meses.Sum(mes => mes.Corrigido);
        DateOnly? corrigidoAte = fatores.Count > 0 ? ultimo : null;
        return new ApuracaoMediaSalarios(periodos, meses, corrigidoAte, faltaDesde, soma, CalculadoraTributacao.Arredondar(soma / meses.Length));
    }
}
