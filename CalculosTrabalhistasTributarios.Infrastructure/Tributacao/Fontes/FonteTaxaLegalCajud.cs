using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>
/// Taxa legal publicada pelo Cajud, usada quando o Banco Central não responde: uma tabela por ano ("Taxa Legal 2026"),
/// com uma linha por mês e a taxa com vírgula decimal; os meses ainda sem taxa vêm com um traço.
/// </summary>
public sealed partial class FonteTaxaLegalCajud : IFonteIndice
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    [GeneratedRegex(@"taxa legal\s+(?<ano>20\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex TituloDoAno { get; }

    public TipoTabelaTributaria Tabela => TipoTabelaTributaria.TaxaLegal;
    public string Nome => "cajud.com.br";
    public bool Oficial => false;
    public Uri Endereco { get; } = new("https://cajud.com.br/tabelas/taxa-legal");

    public async Task<IReadOnlyList<ValorMensalPublicado>> ObterAsync(HttpClient httpClient, DateOnly inicio, CancellationToken cancellationToken)
    {
        var documento = await LeituraDePagina.ObterDocumentoAsync(httpClient, Endereco, cancellationToken);
        var valores = new List<ValorMensalPublicado>();
        foreach (var tabela in documento.DocumentNode.SelectNodes("//table[caption]") ?? Enumerable.Empty<HtmlAgilityPack.HtmlNode>())
        {
            var ano = TituloDoAno.Match(LeituraDePagina.Normalizar(tabela.SelectSingleNode("caption").InnerText));
            if (!ano.Success)
                continue;
            foreach (var linha in tabela.SelectNodes(".//tr[th[@scope='row']]") ?? Enumerable.Empty<HtmlAgilityPack.HtmlNode>())
            {
                var cabecalho = linha.SelectSingleNode("th");
                var nomeDoMes = cabecalho.SelectSingleNode(".//abbr")?.GetAttributeValue("title", "") is { Length: > 0 } titulo ? titulo : cabecalho.InnerText;
                var celula = linha.SelectSingleNode("td");
                if (LeituraDePagina.ObterMes(nomeDoMes.Trim()) is { } mes && celula is not null
                    && decimal.TryParse(LeituraDePagina.Normalizar(celula.InnerText), NumberStyles.Number, Cultura, out var taxa))
                    valores.Add(new(new DateOnly(int.Parse(ano.Groups["ano"].Value, CultureInfo.InvariantCulture), mes, 1), taxa));
            }
        }
        return valores.Where(valor => valor.Competencia >= inicio).OrderBy(valor => valor.Competencia).ToArray();
    }
}
