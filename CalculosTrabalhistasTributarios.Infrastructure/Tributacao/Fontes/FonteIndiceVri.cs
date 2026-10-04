using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>
/// INPC e IPCA publicados pela VRi Consulting, usados quando o IBGE não responde: uma linha por mês ("Ago/2026 | -0,32").
/// </summary>
public sealed partial class FonteIndiceVri(TipoTabelaTributaria tabela) : IFonteIndice
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Meses = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

    [GeneratedRegex(@"^(?<mes>[a-zA-Z]{3})/(?<ano>\d{4})$")]
    private static partial Regex MesAno { get; }

    [GeneratedRegex(@"^-?\d+,\d+$")]
    private static partial Regex Numero { get; }

    public TipoTabelaTributaria Tabela { get; } = tabela;
    public string Nome => "vriconsulting.com.br";
    public bool Oficial => false;

    public Uri Endereco { get; } = new($"https://www.vriconsulting.com.br/indices/{(tabela == TipoTabelaTributaria.Inpc ? "inpc" : "ipca")}.php");

    public async Task<IReadOnlyList<ValorMensalPublicado>> ObterAsync(HttpClient httpClient, DateOnly inicio, CancellationToken cancellationToken)
    {
        var documento = await LeituraDePagina.ObterDocumentoAsync(httpClient, Endereco, cancellationToken);
        var tabela = documento.DocumentNode.SelectSingleNode("//table")
            ?? throw new InvalidOperationException($"A tabela do índice não foi encontrada em {Nome}.");
        var linhas = LeituraDePagina.LerLinhas(tabela);
        return LerMesAMes(linhas).Where(valor => valor.Competencia >= inicio).OrderBy(valor => valor.Competencia).ToArray();
    }

    private static IEnumerable<ValorMensalPublicado> LerMesAMes(IEnumerable<string[]> linhas)
    {
        foreach (var colunas in linhas.Where(colunas => colunas.Length >= 2))
        {
            var mesAno = MesAno.Match(colunas[0].Trim());
            var mes = mesAno.Success ? Array.IndexOf(Meses, mesAno.Groups["mes"].Value.ToLowerInvariant()) : -1;
            if (mes >= 0 && Numero.IsMatch(colunas[1].Trim()))
                yield return new(new DateOnly(int.Parse(mesAno.Groups["ano"].Value, CultureInfo.InvariantCulture), mes + 1, 1), decimal.Parse(colunas[1].Trim(), NumberStyles.Number, Cultura));
        }
    }
}
