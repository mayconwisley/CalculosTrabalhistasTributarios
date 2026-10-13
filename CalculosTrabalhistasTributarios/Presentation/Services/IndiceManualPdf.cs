using System.Text;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Presentation.Services;

/// <summary>
/// Página de cada seção do manual em PDF, lida dos destinos que o gerador grava em cada título. O PDF do QuestPDF guarda os
/// objetos sem compressão de objetos, com o catálogo apontando para a árvore de páginas e para os destinos (/Dests).
/// </summary>
public static partial class IndiceManualPdf
{
    /// <summary>Âncora do título, como "815-jornada-pelo-ponto", e a página (1 = capa); vazio se o PDF não tiver destinos.</summary>
    public static IReadOnlyDictionary<string, int> Ler(byte[] pdf)
    {
        var texto = Encoding.Latin1.GetString(pdf);
        var objetos = new Dictionary<int, string>();
        foreach (Match inicio in InicioObjeto().Matches(texto))
        {
            var numero = int.Parse(inicio.Groups[1].Value);
            var fim = texto.IndexOf("endobj", inicio.Index, StringComparison.Ordinal);
            if (fim > 0 && !objetos.ContainsKey(numero))
                objetos[numero] = texto[inicio.Index..fim];
        }

        var catalogo = objetos.Values.FirstOrDefault(objeto => objeto.Contains("/Type /Catalog", StringComparison.Ordinal));
        if (catalogo is null || Referencia(catalogo, "/Pages") is not { } raiz || Referencia(catalogo, "/Dests") is not { } destinos || !objetos.TryGetValue(destinos, out var dicionario))
            return new Dictionary<string, int>();

        var paginas = new List<int>();
        Coletar(objetos, raiz, paginas, 0);
        var indice = paginas.Select((objeto, posicao) => (objeto, posicao)).GroupBy(item => item.objeto).ToDictionary(grupo => grupo.Key, grupo => grupo.First().posicao + 1);
        var resultado = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match destino in Destino().Matches(dicionario))
        {
            var nome = Nome(destino.Groups[1].Value);
            // O QuestPDF prefixa o nome com o número do documento: "0 | 815-jornada-pelo-ponto".
            var separador = nome.IndexOf("| ", StringComparison.Ordinal);
            if (separador >= 0)
                nome = nome[(separador + 2)..];
            if (indice.TryGetValue(int.Parse(destino.Groups[2].Value), out var pagina))
                resultado.TryAdd(nome, pagina);
        }
        return resultado;
    }

    /// <summary>Página da seção pelo número do título, como "8.15" ou "8.23.1"; nula quando o manual não a tem.</summary>
    public static int? Pagina(IReadOnlyDictionary<string, int> indice, string numero)
    {
        var prefixo = numero.Replace(".", "", StringComparison.Ordinal) + "-";
        foreach (var (ancora, pagina) in indice)
            if (ancora.StartsWith(prefixo, StringComparison.Ordinal))
                return pagina;
        return null;
    }

    private static void Coletar(Dictionary<int, string> objetos, int numero, List<int> paginas, int profundidade)
    {
        if (profundidade > 32 || !objetos.TryGetValue(numero, out var objeto))
            return;
        if (TipoPagina().IsMatch(objeto))
        {
            paginas.Add(numero);
            return;
        }
        var filhos = Filhos().Match(objeto);
        if (!filhos.Success)
            return;
        foreach (Match filho in ReferenciaSolta().Matches(filhos.Groups[1].Value))
            Coletar(objetos, int.Parse(filho.Groups[1].Value), paginas, profundidade + 1);
    }

    private static int? Referencia(string objeto, string chave)
    {
        var encontrada = Regex.Match(objeto, Regex.Escape(chave) + @"\s+(\d+)\s+0\s+R");
        return encontrada.Success ? int.Parse(encontrada.Groups[1].Value) : null;
    }

    /// <summary>Nome PDF com os bytes escapados como #XX, em UTF-8.</summary>
    private static string Nome(string escapado)
    {
        var bytes = new List<byte>(escapado.Length);
        for (var i = 0; i < escapado.Length; i++)
        {
            if (escapado[i] == '#' && i + 2 < escapado.Length && byte.TryParse(escapado.AsSpan(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var valor))
            {
                bytes.Add(valor);
                i += 2;
            }
            else
                bytes.Add((byte)escapado[i]);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    // No início da linha, para não confundir com bytes de um fluxo comprimido.
    [GeneratedRegex(@"^(\d+)\s+0\s+obj\b", RegexOptions.Multiline)]
    private static partial Regex InicioObjeto();

    [GeneratedRegex(@"/Type\s*/Page(?!s)")]
    private static partial Regex TipoPagina();

    [GeneratedRegex(@"/Kids\s*\[([^\]]*)\]")]
    private static partial Regex Filhos();

    [GeneratedRegex(@"(\d+)\s+0\s+R")]
    private static partial Regex ReferenciaSolta();

    [GeneratedRegex(@"/([^\s\[\]/<>()]+)\s*\[\s*(\d+)\s+0\s+R")]
    private static partial Regex Destino();
}
