using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Infrastructure.Tributacao;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class CotacoesTrabalhoExteriorTests
{
    private const string Receita = "<table><tr><th>Mês</th><th>Compra</th><th>Venda</th></tr><tr><td>Setembro</td><td>5,2230</td><td>5,2236</td></tr><tr><td>Outubro</td><td>5,1484</td><td>5,1490</td></tr></table>";
    private const string Euro = "{\"value\":[{\"tipoBoletim\":\"Abertura\",\"cotacaoCompra\":5.9},{\"tipoBoletim\":\"Fechamento PTAX\",\"cotacaoCompra\":6.12}]}";
    private const string Dolar = "{\"value\":[{\"cotacaoCompra\":5.1}]}";

    private static ConsultaCotacoesTrabalhoExterior Consulta(string receita = Receita, string euro = Euro, string dolar = Dolar) =>
        new(() => new HttpClient(new Respostas(uri =>
        {
            if (uri.Host == "www.gov.br") return receita;
            if (uri.AbsolutePath.Contains("CotacaoMoedaDia", StringComparison.Ordinal)) return euro;
            if (uri.AbsolutePath.Contains("CotacaoDolarDia", StringComparison.Ordinal)) return dolar;
            return null;
        })));

    [Fact]
    public async Task Dolar_fiscal_usa_mes_do_recebimento_e_compra_da_Receita()
    {
        var resultado = await Consulta().ConsultarAsync(new DateOnly(2026, 10, 6), "usd", default);
        Assert.True(resultado.Sucesso);
        Assert.Equal(5.1484m, resultado.Valor.DolarCompraFiscal);
        Assert.Equal(1m, resultado.Valor.DolaresPorUnidade);
    }

    [Fact]
    public async Task Outra_moeda_usa_fechamentos_da_mesma_data_como_referencia()
    {
        var resultado = await Consulta().ConsultarAsync(new DateOnly(2026, 10, 6), "EUR", default);
        Assert.True(resultado.Sucesso);
        Assert.Equal(1.2m, resultado.Valor.DolaresPorUnidade);
        Assert.Equal(5.1484m, resultado.Valor.DolarCompraFiscal);
    }

    [Fact]
    public async Task Sem_Ptax_mantem_apenas_dolar_fiscal_disponivel()
    {
        var resultado = await Consulta(euro: "{\"value\":[]}").ConsultarAsync(new DateOnly(2026, 10, 6), "EUR", default);
        Assert.True(resultado.Sucesso);
        Assert.Null(resultado.Valor.DolaresPorUnidade);
    }

    [Fact]
    public async Task Falha_na_fonte_Ptax_nao_descarta_cotacao_ja_lida_da_Receita()
    {
        var resultado = await Consulta(euro: "{\"invalido\":true}").ConsultarAsync(new DateOnly(2026, 10, 6), "EUR", default);
        Assert.True(resultado.Sucesso);
        Assert.Equal(5.1484m, resultado.Valor.DolarCompraFiscal);
        Assert.Null(resultado.Valor.DolaresPorUnidade);
    }

    [Fact]
    public async Task Mes_ainda_nao_publicado_nao_substitui_por_outra_competencia()
    {
        var resultado = await Consulta().ConsultarAsync(new DateOnly(2026, 2, 6), "USD", default);
        Assert.True(resultado.Falhou);
        Assert.Contains("02/2026", resultado.Erro.Mensagem);
    }

    [Fact]
    public void Pais_sugere_moeda_e_preserva_historico_antigo_de_pais_livre()
    {
        var calculadora = new CalculadoraTrabalhoExterior(null!, null!);
        var pais = Assert.IsType<CampoOpcaoViewModel>(calculadora.Campos.Single(campo => campo.Rotulo == "País de origem"));
        pais.Selecionada = pais.Opcoes.Single(opcao => opcao.Texto == "Portugal");
        Assert.Equal("EUR", calculadora.ExportarCampos()["Moeda (ISO)"]);

        calculadora.ImportarCampos(new Dictionary<string, string> { ["País de origem"] = "Noruega", ["Moeda (ISO)"] = "NOK" });
        var campos = calculadora.ExportarCampos();
        Assert.Equal("Outro país", campos["País de origem"]);
        Assert.Equal("Noruega", campos["Outro país (nome)"]);
        Assert.Equal("NOK", campos["Moeda (ISO)"]);
    }

    [Fact]
    public async Task Botao_atualiza_fiscal_e_referencia_sem_alterar_cambio_efetivo()
    {
        var calculadora = new CalculadoraTrabalhoExterior(null!, new ConsultaFixa(
            new CotacoesTrabalhoExteriorDto(5.1484m, 1.1258m, new DateOnly(2026, 10, 2))));
        calculadora.ImportarCampos(new Dictionary<string, string>
        {
            ["Recebimento"] = "02/10/2026", ["País de origem"] = "Portugal", ["Câmbio efetivo"] = "5,50"
        });
        await calculadora.AtualizarCotacoesAsync();
        var campos = calculadora.ExportarCampos();
        Assert.Equal("5,1484", campos["Dólar compra fiscal"]);
        Assert.Equal("1,1258", campos["USD por unidade"]);
        Assert.Equal("5,50", campos["Câmbio efetivo"]);
        Assert.Equal(string.Empty, campos["Cotações online"]);
    }

    [Fact]
    public async Task Cotacao_ausente_limpa_valor_cruzado_antigo()
    {
        var calculadora = new CalculadoraTrabalhoExterior(null!, new ConsultaFixa(
            new CotacoesTrabalhoExteriorDto(5.1484m, null, new DateOnly(2026, 10, 2))));
        calculadora.ImportarCampos(new Dictionary<string, string>
        {
            ["Recebimento"] = "02/10/2026", ["País de origem"] = "Portugal", ["USD por unidade"] = "1,5"
        });
        await calculadora.AtualizarCotacoesAsync();
        Assert.Equal("0", calculadora.ExportarCampos()["USD por unidade"]);
    }

    private sealed class ConsultaFixa(CotacoesTrabalhoExteriorDto resposta) : IConsultaCotacoesTrabalhoExterior
    {
        public Task<Result<CotacoesTrabalhoExteriorDto>> ConsultarAsync(DateOnly recebimento, string moeda, CancellationToken cancellationToken) =>
            Task.FromResult(Result<CotacoesTrabalhoExteriorDto>.Ok(resposta));
    }

    private sealed class Respostas(Func<Uri, string?> obter) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var conteudo = obter(request.RequestUri ?? throw new InvalidOperationException());
            return Task.FromResult(new HttpResponseMessage(conteudo is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new StringContent(conteudo ?? string.Empty, Encoding.UTF8)
            });
        }
    }
}
