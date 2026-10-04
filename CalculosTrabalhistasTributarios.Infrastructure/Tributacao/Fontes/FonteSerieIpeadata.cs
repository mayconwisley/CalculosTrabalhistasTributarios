using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>
/// Selic e TR mensais republicadas pelo Ipeadata (Ipea), a partir do Banco Central, usadas quando o Banco Central não
/// responde: séries BM12_TJOVER12 (Selic acumulada no mês) e BM12_TJTR12 (TR do primeiro dia do mês).
/// </summary>
public sealed class FonteSerieIpeadata(TipoTabelaTributaria tabela, string codigo) : IFonteIndice
{
    public TipoTabelaTributaria Tabela { get; } = tabela;
    public string Nome => "Ipeadata";
    public bool Oficial => false;
    public Uri Endereco { get; } = new($"https://www.ipeadata.gov.br/ExibeSerie.aspx?serid={codigo}");

    public async Task<IReadOnlyList<ValorMensalPublicado>> ObterAsync(HttpClient httpClient, DateOnly inicio, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(await httpClient.GetStringAsync(new Uri($"https://www.ipeadata.gov.br/api/odata4/ValoresSerie(SERCODIGO='{codigo}')"), cancellationToken));
        // Cada item traz VALDATA (data com hora e fuso, começando por AAAA-MM-DD) e VALVALOR (número ou nulo).
        return json.RootElement.GetProperty("value").EnumerateArray()
            .Where(item => item.GetProperty("VALVALOR").ValueKind == JsonValueKind.Number)
            .Select(item =>
            {
                var data = DateOnly.ParseExact(item.GetProperty("VALDATA").GetString()![..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                return new ValorMensalPublicado(new DateOnly(data.Year, data.Month, 1), item.GetProperty("VALVALOR").GetDecimal());
            })
            .Where(valor => valor.Competencia >= inicio && FonteSerieBancoCentral.MesCompleto(Tabela, valor.Competencia))
            .ToArray();
    }
}
