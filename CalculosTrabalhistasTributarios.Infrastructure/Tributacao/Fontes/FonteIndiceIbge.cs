using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;

/// <summary>INPC, IPCA e IPCA-15 (o IPCA-E mensal) pela API do Sidra, do IBGE, que calcula e publica os índices.</summary>
public sealed class FonteIndiceIbge(TipoTabelaTributaria tabela) : IFonteIndice
{
    public TipoTabelaTributaria Tabela { get; } = tabela;
    public string Nome => "IBGE";
    public bool Oficial => true;

    public Uri Endereco { get; } = new(tabela switch
    {
        TipoTabelaTributaria.Inpc => "https://www.ibge.gov.br/estatisticas/economicas/precos-e-custos/9258-indice-nacional-de-precos-ao-consumidor.html",
        TipoTabelaTributaria.IpcaE => "https://www.ibge.gov.br/estatisticas/economicas/precos-e-custos/9260-indice-nacional-de-precos-ao-consumidor-amplo-15.html",
        _ => "https://www.ibge.gov.br/estatisticas/economicas/precos-e-custos/9256-indice-nacional-de-precos-ao-consumidor-amplo.html"
    });

    public async Task<IReadOnlyList<ValorMensalPublicado>> ObterAsync(HttpClient httpClient, DateOnly inicio, CancellationToken cancellationToken)
    {
        // Tabela 1736, variável 44: INPC; tabela 1737, variável 63: IPCA; tabela 3065, variável 355: IPCA-15. Todas, variação mensal.
        var (tabelaSidra, variavel) = Tabela switch
        {
            TipoTabelaTributaria.Inpc => (1736, 44),
            TipoTabelaTributaria.IpcaE => (3065, 355),
            _ => (1737, 63)
        };
        var endereco = new Uri($"https://apisidra.ibge.gov.br/values/t/{tabelaSidra}/n1/all/v/{variavel}/p/{inicio:yyyyMM}-{DateTime.Today.Year}12");
        using var json = JsonDocument.Parse(await httpClient.GetStringAsync(endereco, cancellationToken));
        // O primeiro item é o cabeçalho; nos demais, D3C é o mês (AAAAMM) e V, a variação com ponto decimal.
        return json.RootElement.EnumerateArray().Skip(1)
            .Select(item => (Mes: item.GetProperty("D3C").GetString(), Valor: item.GetProperty("V").GetString()))
            .Where(item => item.Mes is { Length: 6 } && decimal.TryParse(item.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            .Select(item => new ValorMensalPublicado(
                new DateOnly(int.Parse(item.Mes![..4], CultureInfo.InvariantCulture), int.Parse(item.Mes[4..], CultureInfo.InvariantCulture), 1),
                decimal.Parse(item.Valor!, NumberStyles.Number, CultureInfo.InvariantCulture)))
            .ToArray();
    }
}
