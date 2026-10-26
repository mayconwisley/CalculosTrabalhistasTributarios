using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Presentation.Documentos;

/// <summary>
/// Termos de uso, licença, responsabilidade e privacidade, escritos em docs/legal e embutidos no aplicativo do Windows e no do
/// Android, que mostram o mesmo texto. A licença inclui LICENSE e THIRD-PARTY-NOTICES.md pela linha "&lt;!-- incluir: arquivo --&gt;",
/// para os avisos de terceiros terem uma só fonte.
/// </summary>
public static partial class DocumentosLegais
{
    private const string PrefixoRecurso = "DocumentosLegais.";
    private static readonly ConcurrentDictionary<TipoDocumentoLegal, IReadOnlyList<BlocoDocumentoLegal>> Lidos = new();

    public static IReadOnlyList<DocumentoLegal> Lista { get; } =
    [
        new(TipoDocumentoLegal.TermosDeUso, "Termos de Uso", "Condições para usar o aplicativo, as tabelas e as atualizações.", "TERMOS-DE-USO.md"),
        new(TipoDocumentoLegal.LicencaDeUso, "Licença de uso", "Licença MIT do aplicativo e licenças dos componentes de terceiros.", "LICENCA-DE-USO.md"),
        new(TipoDocumentoLegal.ResponsabilidadeDeUso, "Responsabilidade de uso", "Cálculo simulado, valores estimados e o papel do profissional especializado.", "RESPONSABILIDADE-DE-USO.md"),
        new(TipoDocumentoLegal.PoliticaDePrivacidade, "Política de Privacidade", "Dados guardados só no computador ou no aparelho e quando a internet é usada.", "POLITICA-DE-PRIVACIDADE.md")
    ];

    public static DocumentoLegal Documento(TipoDocumentoLegal tipo) => Lista.Single(documento => documento.Tipo == tipo);

    /// <summary>O título do arquivo fica de fora: a tela mostra o título do documento no cabeçalho.</summary>
    public static IReadOnlyList<BlocoDocumentoLegal> Blocos(TipoDocumentoLegal tipo) =>
        Lidos.GetOrAdd(tipo, chave =>
        {
            var blocos = new List<BlocoDocumentoLegal>();
            LerMarkdown(Documento(chave).Arquivo, nivelExtra: 0, blocos);
            return blocos;
        });

    private static string Ler(string arquivo)
    {
        using var stream = typeof(DocumentosLegais).Assembly.GetManifestResourceStream(PrefixoRecurso + arquivo)
            ?? throw new InvalidOperationException($"Documento embutido ausente: {arquivo}.");
        using var leitor = new StreamReader(stream);
        return leitor.ReadToEnd().Replace("\r\n", "\n");
    }

    /// <param name="nivelExtra">Rebaixa os títulos de um arquivo incluído, cujo "#" vira uma seção do documento.</param>
    private static void LerMarkdown(string arquivo, int nivelExtra, List<BlocoDocumentoLegal> blocos)
    {
        var linhas = Ler(arquivo).Split('\n');
        var paragrafo = new List<string>();
        void FecharParagrafo(TipoBlocoDocumentoLegal tipo = TipoBlocoDocumentoLegal.Paragrafo)
        {
            if (paragrafo.Count > 0) blocos.Add(new(tipo, Limpar(string.Join(' ', paragrafo))));
            paragrafo.Clear();
        }

        var emCodigo = false;
        for (var i = 0; i < linhas.Length; i++)
        {
            var linha = linhas[i].Trim();
            if (linha.StartsWith("```", StringComparison.Ordinal))
            {
                FecharParagrafo(emCodigo ? TipoBlocoDocumentoLegal.TextoLicenca : TipoBlocoDocumentoLegal.Paragrafo);
                emCodigo = !emCodigo;
            }
            // O texto das licenças vem quebrado em linhas curtas; os parágrafos são separados por linha em branco.
            else if (emCodigo)
            {
                if (linha.Length == 0) FecharParagrafo(TipoBlocoDocumentoLegal.TextoLicenca);
                else paragrafo.Add(linha);
            }
            else if (Inclusao().Match(linha) is { Success: true } inclusao)
            {
                FecharParagrafo();
                var incluido = inclusao.Groups[1].Value;
                if (incluido.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) LerMarkdown(incluido, nivelExtra + 1, blocos);
                else LerTextoSimples(incluido, blocos);
            }
            else if (Titulo().Match(linha) is { Success: true } titulo)
            {
                FecharParagrafo();
                var nivel = titulo.Groups[1].Length + nivelExtra;
                if (nivel > 1)
                    blocos.Add(new(nivel == 2 ? TipoBlocoDocumentoLegal.Secao : TipoBlocoDocumentoLegal.Subsecao, Limpar(titulo.Groups[2].Value)));
            }
            else if (linha.Length == 0 || Separador().IsMatch(linha)) FecharParagrafo();
            else if (linha.StartsWith('|'))
            {
                FecharParagrafo();
                // O cabeçalho da tabela só nomeia as colunas; cada linha vira um item "**primeira coluna**: demais".
                if (i + 1 < linhas.Length && Separador().IsMatch(linhas[i + 1].Trim())) continue;
                var celulas = linha.Trim('|').Split('|').Select(celula => celula.Trim()).ToArray();
                var texto = celulas.Length == 1 ? celulas[0] : $"**{celulas[0].Replace("**", "")}**: {string.Join(" — ", celulas[1..])}";
                blocos.Add(new(TipoBlocoDocumentoLegal.Item, Limpar(texto)));
            }
            else if (ItemDeLista().Match(linha) is { Success: true } item)
            {
                FecharParagrafo();
                blocos.Add(new(TipoBlocoDocumentoLegal.Item, Limpar(item.Groups[1].Value)));
            }
            else paragrafo.Add(linha.TrimStart('>', ' '));
        }
        FecharParagrafo(emCodigo ? TipoBlocoDocumentoLegal.TextoLicenca : TipoBlocoDocumentoLegal.Paragrafo);
    }

    private static void LerTextoSimples(string arquivo, List<BlocoDocumentoLegal> blocos)
    {
        foreach (var paragrafo in Ler(arquivo).Split("\n\n"))
        {
            var texto = string.Join(' ', paragrafo.Split('\n').Select(linha => linha.Trim()).Where(linha => linha.Length > 0));
            if (texto.Length > 0) blocos.Add(new(TipoBlocoDocumentoLegal.TextoLicenca, texto));
        }
    }

    // Links viram o texto do link, endereços entre "<>" ficam visíveis e o código perde as crases; o negrito fica para a tela.
    private static string Limpar(string texto) => Codigo().Replace(Endereco().Replace(Link().Replace(texto, "$1"), "$1"), "$1");

    [GeneratedRegex(@"^<!--\s*incluir:\s*([\w.-]+)\s*-->$")]
    private static partial Regex Inclusao();

    [GeneratedRegex(@"^(#{1,4}) (.+)$")]
    private static partial Regex Titulo();

    [GeneratedRegex(@"^(?:[-*]|\d+\.) (.+)$")]
    private static partial Regex ItemDeLista();

    [GeneratedRegex(@"^\|?\s*:?-{3,}")]
    private static partial Regex Separador();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"<(https?://[^>]+)>")]
    private static partial Regex Endereco();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex Codigo();
}
