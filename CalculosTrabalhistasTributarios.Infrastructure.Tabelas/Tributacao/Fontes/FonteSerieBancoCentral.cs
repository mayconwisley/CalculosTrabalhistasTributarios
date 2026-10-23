using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>
/// Uma série mensal do Sistema Gerenciador de Séries (SGS) do Banco Central: a taxa legal (29543), a Selic acumulada no
/// mês (4390) e a TR do primeiro dia do mês (7811).
/// </summary>
public class FonteSerieBancoCentral(TipoTabelaTributaria tabela, int serie, string pagina) : IFonteIndice
{
    public TipoTabelaTributaria Tabela { get; } = tabela;
    public string Nome => "Banco Central";
    public bool Oficial => true;
    public Uri Endereco { get; } = new(pagina);

    public async Task<IReadOnlyList<ValorMensalPublicado>> ObterAsync(HttpClient httpClient, DateOnly inicio, CancellationToken cancellationToken)
    {
        var endereco = new Uri($"https://api.bcb.gov.br/dados/serie/bcdata.sgs.{serie}/dados?formato=json&dataInicial={inicio:dd'/'MM'/'yyyy}&dataFinal={DateTime.Today:dd'/'MM'/'yyyy}");
        using var json = JsonDocument.Parse(await httpClient.GetStringAsync(endereco, cancellationToken));
        // Cada item traz a data do primeiro dia do mês (dd/MM/aaaa) e a taxa com ponto decimal.
        return json.RootElement.EnumerateArray()
            .Select(item => (Data: item.GetProperty("data").GetString(), Valor: item.GetProperty("valor").GetString()))
            .Where(item => DateOnly.TryParseExact(item.Data, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                && decimal.TryParse(item.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            .Select(item =>
            {
                var data = DateOnly.ParseExact(item.Data!, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                return new ValorMensalPublicado(new DateOnly(data.Year, data.Month, 1), decimal.Parse(item.Valor!, NumberStyles.Number, CultureInfo.InvariantCulture));
            })
            .Where(valor => MesCompleto(Tabela, valor.Competencia))
            .ToArray();
    }

    /// <summary>A Selic do mês em curso ainda está acumulando e só vale quando o mês termina.</summary>
    public static bool MesCompleto(TipoTabelaTributaria tabela, DateOnly competencia) =>
        tabela != TipoTabelaTributaria.Selic || competencia < new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
}
