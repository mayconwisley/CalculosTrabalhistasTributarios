using CalculosTrabalhistasTributarios.Application.DTOs;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

/// <summary>
/// Linhas da comparação de dois demonstrativos da mesma calculadora: destaques, quadro comparativo, proventos, descontos,
/// totais e informativos, com a diferença do segundo para o primeiro quando os dois são valores em reais. Verbas com a
/// mesma descrição (ou a mesma <see cref="VerbaDto.ChaveComparacao"/>) no mesmo grupo são pareadas pela ordem em que aparecem.
/// </summary>
public static class ComparacaoResultados
{
    private const string Ausente = "—";
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static IReadOnlyList<LinhaResultadoComparadoViewModel> Linhas(DemonstrativoDto primeiro, DemonstrativoDto segundo)
    {
        var linhas = new List<LinhaResultadoComparadoViewModel>();
        Textos(linhas, "Resumo", primeiro.Destaques.Select(destaque => (destaque.Rotulo, destaque.Valor)), segundo.Destaques.Select(destaque => (destaque.Rotulo, destaque.Valor)));
        if (primeiro.Comparativo is { } tabelaPrimeiro && segundo.Comparativo is { } tabelaSegundo)
            Textos(linhas, tabelaPrimeiro.Titulo, Celulas(tabelaPrimeiro), Celulas(tabelaSegundo));
        Valores(linhas, primeiro.RotuloProventos, primeiro.Proventos, segundo.Proventos);
        Valores(linhas, "Descontos", primeiro.Descontos, segundo.Descontos);
        if (primeiro.TemVerbas || segundo.TemVerbas)
        {
            linhas.Add(Linha("Totais", $"Total de {primeiro.RotuloProventos.ToLower(Cultura)}", primeiro.TotalProventos, segundo.TotalProventos));
            linhas.Add(Linha("Totais", "Total de descontos", primeiro.TotalDescontos, segundo.TotalDescontos));
            linhas.Add(Linha("Totais", primeiro.RotuloResultado, primeiro.Resultado, segundo.Resultado));
        }
        Valores(linhas, "Informativos", primeiro.Informativos, segundo.Informativos);
        return linhas;
    }

    /// <summary>"R$ 1.234,56", "-R$ 10,00" ou "+R$ 5,00" como número; nulo para outros textos, como "12/12" ou "27,5%".</summary>
    public static decimal? LerMoeda(string texto)
    {
        var valor = texto.Replace(' ', ' ').Trim();
        var negativo = valor.StartsWith('-');
        if (negativo || valor.StartsWith('+'))
            valor = valor[1..].TrimStart();
        if (!valor.StartsWith("R$", StringComparison.Ordinal)
            || !decimal.TryParse(valor[2..].Trim(), NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, Cultura, out var numero))
            return null;
        return negativo ? -numero : numero;
    }

    private static IEnumerable<(string Item, string Valor)> Celulas(TabelaComparativaDto tabela) =>
        tabela.Linhas.SelectMany(linha => linha.Valores.Select((valor, coluna) => ($"{linha.Descricao} · {tabela.Colunas.ElementAtOrDefault(coluna)}", valor)));

    private static void Textos(List<LinhaResultadoComparadoViewModel> linhas, string grupo, IEnumerable<(string Item, string Valor)> primeiro, IEnumerable<(string Item, string Valor)> segundo)
    {
        foreach (var par in Parear(primeiro.Select(item => (item.Item, item.Item, item.Valor)).ToArray(), segundo.Select(item => (item.Item, item.Item, item.Valor)).ToArray()))
        {
            var diferenca = par.TemPrimeiro && par.TemSegundo && LerMoeda(par.Primeiro!) is { } numeroA && LerMoeda(par.Segundo!) is { } numeroB ? Diferenca(numeroA, numeroB) : string.Empty;
            linhas.Add(new(grupo, par.Item, par.TemPrimeiro ? par.Primeiro! : Ausente, par.TemSegundo ? par.Segundo! : Ausente, diferenca));
        }
    }

    private static void Valores(List<LinhaResultadoComparadoViewModel> linhas, string grupo, IReadOnlyList<VerbaDto> primeiro, IReadOnlyList<VerbaDto> segundo)
    {
        foreach (var par in Parear(Itens(primeiro), Itens(segundo)))
            linhas.Add(Linha(grupo, par.Item, par.TemPrimeiro ? par.Primeiro : null, par.TemSegundo ? par.Segundo : null));
    }

    private static LinhaResultadoComparadoViewModel Linha(string grupo, string item, decimal? primeiro, decimal? segundo) =>
        new(grupo, item, primeiro is { } a ? Moeda(a) : Ausente, segundo is { } b ? Moeda(b) : Ausente, Diferenca(primeiro ?? 0m, segundo ?? 0m));

    private static (string Chave, string Item, decimal Valor)[] Itens(IReadOnlyList<VerbaDto> verbas) =>
        [.. verbas.Select(verba => (verba.ChaveComparacao ?? verba.Descricao, verba.Descricao, verba.Valor))];

    /// <summary>Junta pela chave, na ordem do primeiro e depois os que só o segundo tem; chaves repetidas, pela ocorrência.</summary>
    private static IEnumerable<(string Item, bool TemPrimeiro, T Primeiro, bool TemSegundo, T Segundo)> Parear<T>(IReadOnlyList<(string Chave, string Item, T Valor)> primeiro, IReadOnlyList<(string Chave, string Item, T Valor)> segundo)
    {
        static List<(string Chave, string Item, T Valor)> Chaves(IReadOnlyList<(string Chave, string Item, T Valor)> itens)
        {
            var vistos = new Dictionary<string, int>(StringComparer.Ordinal);
            return itens.Select(item =>
            {
                var ocorrencia = vistos[item.Chave] = vistos.GetValueOrDefault(item.Chave) + 1;
                return ($"{item.Chave}\u0001{ocorrencia}", item.Item, item.Valor);
            }).ToList();
        }
        var a = Chaves(primeiro);
        var b = Chaves(segundo).ToDictionary(item => item.Chave);
        foreach (var item in a)
        {
            var temSegundo = b.Remove(item.Chave, out var outro);
            yield return (temSegundo ? Juntar(item.Item, outro.Item) : item.Item, true, item.Valor, temSegundo, temSegundo ? outro.Valor : default!);
        }
        foreach (var item in Chaves(segundo).Where(item => b.ContainsKey(item.Chave)))
            yield return (item.Item, false, default!, true, item.Valor);
    }

    /// <summary>
    /// Linhas pareadas pela posição podem ter nomes diferentes: "1º vínculo: Empresa A → Empresa B" mostra os dois sem
    /// repetir o início comum, cortado depois de ": " ou " - " para não partir um nome ao meio.
    /// </summary>
    private static string Juntar(string primeiro, string segundo)
    {
        if (primeiro == segundo) return primeiro;
        var comum = 0;
        while (comum < Math.Min(primeiro.Length, segundo.Length) && primeiro[comum] == segundo[comum]) comum++;
        var prefixo = primeiro[..comum];
        var doisPontos = prefixo.LastIndexOf(": ", StringComparison.Ordinal);
        var traco = prefixo.LastIndexOf(" - ", StringComparison.Ordinal);
        var fim = Math.Max(doisPontos < 0 ? -1 : doisPontos + 2, traco < 0 ? -1 : traco + 3);
        return fim < 0 ? $"{primeiro} → {segundo}" : $"{primeiro} → {segundo[fim..]}";
    }

    private static string Diferenca(decimal primeiro, decimal segundo) => (segundo - primeiro) switch
    {
        > 0m and var diferenca => "+" + Moeda(diferenca),
        < 0m and var diferenca => "-" + Moeda(-diferenca),
        _ => "sem diferença"
    };

    private static string Moeda(decimal valor) => valor.ToString("C2", Cultura);
}
