using System.Text.RegularExpressions;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Presentation;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Ajuda;

/// <summary>
/// Seção do manual (docs/MANUAL.md, embutido no assembly) de cada calculadora, pela mesma numeração do botão "?" do desktop.
/// O leitor de PDF do Android não aceita abrir numa página, por isso a ajuda mostra o trecho dentro do aplicativo.
/// </summary>
public static partial class ManualMobile
{
    /// <summary>Pasta dos prints de docs/imagens dentro do pacote do aplicativo.</summary>
    public const string PastaImagens = "manual";

    private static readonly Lazy<string[]> Linhas = new(() =>
    {
        using var stream = typeof(ManualMobile).Assembly.GetManifestResourceStream("Manual.MANUAL.md")
            ?? throw new InvalidOperationException("Manual embutido ausente.");
        using var leitor = new StreamReader(stream);
        return leitor.ReadToEnd().Replace("\r\n", "\n").Split('\n');
    });

    /// <summary>Chave do desktop: algumas janelas não seguem "Calculadora.{Tipo}" ou têm outro nome.</summary>
    public static string ChaveDesktop(TipoCalculadoraMobile tipo) => tipo switch
    {
        TipoCalculadoraMobile.ProLabore => "Calculadora.ProLaboreAutonomo",
        TipoCalculadoraMobile.PensaoAlimenticia => "Pensao",
        TipoCalculadoraMobile.PensaoAtraso or TipoCalculadoraMobile.DebitoJudicial
            or TipoCalculadoraMobile.Estabilidade or TipoCalculadoraMobile.Jornada => tipo.ToString(),
        _ => $"Calculadora.{tipo}"
    };

    public static string? NumeroSecao(TipoCalculadoraMobile tipo) => SecoesDoManual.Numero(ChaveDesktop(tipo));

    /// <summary>A seção e suas subseções, até o próximo título do mesmo nível; vazio quando o número não existe.</summary>
    public static IReadOnlyList<BlocoManualMobile> Secao(string numero)
    {
        var linhas = Linhas.Value;
        var inicio = Array.FindIndex(linhas, linha => Titulo(linha) is { } titulo && titulo.Numero == numero);
        if (inicio < 0) return [];
        var nivel = Titulo(linhas[inicio])!.Value.Nivel;
        var blocos = new List<BlocoManualMobile>();
        var paragrafo = new List<string>();
        void FecharParagrafo()
        {
            if (paragrafo.Count > 0) blocos.Add(new(TipoBlocoManualMobile.Paragrafo, Limpar(string.Join(' ', paragrafo))));
            paragrafo.Clear();
        }

        for (var i = inicio; i < linhas.Length; i++)
        {
            var linha = linhas[i].Trim();
            if (Titulo(linha) is { } titulo)
            {
                if (i > inicio && titulo.Nivel <= nivel) break;
                FecharParagrafo();
                blocos.Add(new(i == inicio ? TipoBlocoManualMobile.Titulo : TipoBlocoManualMobile.Subtitulo, Limpar(titulo.Texto)));
            }
            else if (Imagem().Match(linha) is { Success: true } imagem)
            {
                FecharParagrafo();
                blocos.Add(new(TipoBlocoManualMobile.Imagem, imagem.Groups[1].Value, $"{PastaImagens}/{imagem.Groups[2].Value}"));
            }
            else if (linha.Length == 0 || linha.StartsWith("![", StringComparison.Ordinal) || Separador().IsMatch(linha)) FecharParagrafo();
            else if (linha.StartsWith('|'))
            {
                FecharParagrafo();
                // Na tabela, o cabeçalho só nomeia as colunas; cada linha vira um item "**primeira coluna**: demais".
                if (i + 1 < linhas.Length && Separador().IsMatch(linhas[i + 1].Trim())) continue;
                var celulas = linha.Trim('|').Split('|').Select(celula => celula.Trim()).ToArray();
                var texto = celulas.Length == 1 ? celulas[0] : $"**{celulas[0].Replace("**", "")}**: {string.Join(" — ", celulas[1..])}";
                blocos.Add(new(TipoBlocoManualMobile.Item, Limpar(texto)));
            }
            else if (ItemDeLista().Match(linha) is { Success: true } item)
            {
                FecharParagrafo();
                blocos.Add(new(TipoBlocoManualMobile.Item, Limpar(item.Groups[1].Value)));
            }
            else if (linha.StartsWith('>'))
            {
                FecharParagrafo();
                blocos.Add(new(TipoBlocoManualMobile.Destaque, Limpar(linha.TrimStart('>', ' '))));
            }
            else paragrafo.Add(linha);
        }
        FecharParagrafo();
        return blocos;
    }

    private static (int Nivel, string Numero, string Texto)? Titulo(string linha)
    {
        var resultado = TituloNumerado().Match(linha.Trim());
        return resultado.Success
            ? (resultado.Groups[1].Length, resultado.Groups[2].Value, $"{resultado.Groups[2].Value} {resultado.Groups[3].Value}")
            : null;
    }

    // Links viram o texto do link e o código perde as crases; o negrito fica para a tela destacar.
    private static string Limpar(string texto) => Codigo().Replace(Link().Replace(texto, "$1"), "$1");

    [GeneratedRegex(@"^(#{2,4}) (\d+(?:\.\d+)*)\.? (.+)$")]
    private static partial Regex TituloNumerado();

    // Os prints do manual ficam em docs/imagens, referenciados como "![descrição](imagens/arquivo.png)".
    [GeneratedRegex(@"^!\[([^\]]*)\]\(imagens/([\w.-]+\.png)\)$")]
    private static partial Regex Imagem();

    [GeneratedRegex(@"^(?:[-*]|\d+\.) (.+)$")]
    private static partial Regex ItemDeLista();

    [GeneratedRegex(@"^\|?\s*:?-{3,}")]
    private static partial Regex Separador();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex Codigo();
}
