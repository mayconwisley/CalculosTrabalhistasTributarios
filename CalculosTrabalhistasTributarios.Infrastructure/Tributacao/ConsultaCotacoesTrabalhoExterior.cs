using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using HtmlAgilityPack;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>Consulta a compra fiscal da Receita e a PTAX de fechamento como referência para moedas não USD.</summary>
public sealed class ConsultaCotacoesTrabalhoExterior(Func<HttpClient> criarCliente) : IConsultaCotacoesTrabalhoExterior
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private const string Receita = "https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/tabelas/conversao/";
    private const string Ptax = "https://olinda.bcb.gov.br/olinda/servico/PTAX/versao/v1/odata/";

    public async Task<Result<CotacoesTrabalhoExteriorDto>> ConsultarAsync(DateOnly recebimento, string moeda, CancellationToken cancellationToken)
    {
        moeda = moeda.Trim().ToUpperInvariant();
        if (recebimento.Year < 2020 || recebimento > DateOnly.FromDateTime(DateTime.Today) ||
            moeda.Length != 3 || moeda.Any(letra => letra is < 'A' or > 'Z'))
            return Erro.Validacao("Confira recebimento e moeda: informe uma data já ocorrida desde 2020 e um código ISO de três letras.");

        try
        {
            using var cliente = criarCliente();
            var fiscal = await LerDolarFiscalAsync(cliente, recebimento, cancellationToken);
            if (fiscal is null)
                return Erro.NaoEncontrado($"A Receita ainda não publicou o dólar compra fiscal de {recebimento:MM/yyyy}. Informe a cotação oficial manualmente.");

            if (moeda == "USD")
                return new CotacoesTrabalhoExteriorDto(fiscal.Value, 1m, recebimento);

            var data = recebimento.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
            var moedaUrl = $"{Ptax}CotacaoMoedaDia(moeda=@moeda,dataCotacao=@dataCotacao)?%40moeda='{moeda}'&%40dataCotacao='{data}'&%24format=json";
            var dolarUrl = $"{Ptax}CotacaoDolarDia(dataCotacao=@dataCotacao)?%40dataCotacao='{data}'&%24format=json";
            decimal? cotacaoMoeda;
            decimal? cotacaoDolar;
            try
            {
                cotacaoMoeda = await LerFechamentoAsync(cliente, moedaUrl, true, cancellationToken);
                cotacaoDolar = await LerFechamentoAsync(cliente, dolarUrl, false, cancellationToken);
            }
            catch (HttpRequestException)
            {
                return new CotacoesTrabalhoExteriorDto(fiscal.Value, null, recebimento);
            }
            catch (JsonException)
            {
                return new CotacoesTrabalhoExteriorDto(fiscal.Value, null, recebimento);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new CotacoesTrabalhoExteriorDto(fiscal.Value, null, recebimento);
            }
            if (cotacaoMoeda is null || cotacaoDolar is null)
                return new CotacoesTrabalhoExteriorDto(fiscal.Value, null, recebimento);

            // As duas cotações são BRL por unidade no mesmo fechamento. O quociente é USD por unidade,
            // apenas como referência: a regra fiscal exige a autoridade monetária do país de origem.
            var cruzada = decimal.Round(cotacaoMoeda.Value / cotacaoDolar.Value, 6, MidpointRounding.AwayFromZero);
            return cruzada is > 0m and <= 100_000m
                ? new CotacoesTrabalhoExteriorDto(fiscal.Value, cruzada, recebimento)
                : new CotacoesTrabalhoExteriorDto(fiscal.Value, null, recebimento);
        }
        catch (HttpRequestException)
        {
            return Erro.Indisponivel("Não foi possível consultar Receita ou Banco Central. Confira a conexão e tente novamente; as cotações podem ser informadas manualmente.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Erro.Indisponivel("A consulta de cotações demorou demais. Tente novamente ou informe as cotações manualmente.");
        }
        catch (JsonException)
        {
            return Erro.Indisponivel("O Banco Central retornou cotações em formato inesperado. Informe USD por unidade manualmente e confira a fonte oficial.");
        }
    }

    private static async Task<decimal?> LerDolarFiscalAsync(HttpClient cliente, DateOnly recebimento, CancellationToken cancellationToken)
    {
        var html = await cliente.GetStringAsync($"{Receita}{recebimento.Year}", cancellationToken);
        var documento = new HtmlDocument();
        documento.LoadHtml(html);
        var mes = Cultura.DateTimeFormat.GetMonthName(recebimento.Month);
        foreach (var tabela in documento.DocumentNode.SelectNodes("//table") ?? Enumerable.Empty<HtmlNode>())
        {
            var linhas = tabela.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>();
            foreach (var linha in linhas)
            {
                var celulas = (linha.SelectNodes("./th|./td") ?? Enumerable.Empty<HtmlNode>())
                    .Select(celula => WebUtility.HtmlDecode(celula.InnerText).Trim().Replace('\u00a0', ' ')).ToArray();
                if (celulas.Length < 3 || !string.Equals(celulas[0], mes, StringComparison.OrdinalIgnoreCase))
                    continue;
                return decimal.TryParse(celulas[1], NumberStyles.Number, Cultura, out var valor) && valor is > 0m and <= 100_000m
                    && decimal.Round(valor, 6) == valor ? valor : null;
            }
        }
        return null;
    }

    private static async Task<decimal?> LerFechamentoAsync(HttpClient cliente, string endereco, bool exigeBoletim, CancellationToken cancellationToken)
    {
        await using var resposta = await cliente.GetStreamAsync(endereco, cancellationToken);
        using var documento = await JsonDocument.ParseAsync(resposta, cancellationToken: cancellationToken);
        if (!documento.RootElement.TryGetProperty("value", out var valores) || valores.ValueKind != JsonValueKind.Array)
            throw new JsonException("Lista de cotações ausente.");
        foreach (var item in valores.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            if (exigeBoletim && (!item.TryGetProperty("tipoBoletim", out var boletim) ||
                boletim.ValueKind != JsonValueKind.String || boletim.GetString() is not { } tipo ||
                !tipo.Contains("Fechamento", StringComparison.OrdinalIgnoreCase)))
                continue;
            if (!item.TryGetProperty("cotacaoCompra", out var compra) || compra.ValueKind != JsonValueKind.Number ||
                !compra.TryGetDecimal(out var valor))
                continue;
            if (valor is > 0m and <= 100_000m)
                return valor;
        }
        return null;
    }
}
