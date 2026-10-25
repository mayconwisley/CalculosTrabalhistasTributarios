using System.Text.Json;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class CopiaSegurancaHistoricoTests : IDisposable
{
    private const string Origem = "Android";
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "copia-historico-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private SqliteHistoricoCalculos Criar(string nome)
    {
        Directory.CreateDirectory(_pasta);
        return new(new BancoTributario(new SqliteConnectionStringBuilder { DataSource = Path.Combine(_pasta, nome), Pooling = false }.ToString()));
    }

    private static CopiaSegurancaHistoricoUseCase CasoDeUso(SqliteHistoricoCalculos historico) => new(historico, historico);

    [Fact]
    public async Task Restaura_num_banco_novo_com_nomes_formularios_e_datas_e_nao_duplica_ao_repetir()
    {
        var aparelhoAntigo = Criar("antigo.db");
        await aparelhoAntigo.SalvarAsync(null, "Calculadora.Ferias", "Férias", "Férias de julho", "{\"Versao\":1}", Cancelamento);
        await aparelhoAntigo.SalvarAsync(null, "Calculadora.Rescisao", "Rescisão", "Rescisão sem justa causa", "{\"Versao\":2}", Cancelamento);
        var originais = await aparelhoAntigo.ListarAsync(Cancelamento);
        var copia = await CasoDeUso(aparelhoAntigo).ExportarAsync(Origem, new DateTime(2026, 10, 25, 14, 30, 0), Cancelamento);
        Assert.True(copia.Sucesso);
        Assert.Equal("historico-calculos-20261025-1430.json", copia.Valor.NomeArquivo);
        Assert.Equal(2, copia.Valor.Quantidade);

        var aparelhoNovo = Criar("novo.db");
        var primeira = await CasoDeUso(aparelhoNovo).RestaurarAsync(copia.Valor.Conteudo, Origem, Cancelamento);
        var segunda = await CasoDeUso(aparelhoNovo).RestaurarAsync(copia.Valor.Conteudo, Origem, Cancelamento);

        Assert.Equal(new ResumoRestauracaoHistoricoDto(2, 0), primeira.Valor);
        Assert.Equal(new ResumoRestauracaoHistoricoDto(0, 2), segunda.Valor);
        var restaurados = await aparelhoNovo.ListarAsync(Cancelamento);
        Assert.Equal(originais.Select(c => (c.Tipo, c.Calculadora, c.Nome, c.Dados, c.CriadoEm, c.AlteradoEm)),
            restaurados.Select(c => (c.Tipo, c.Calculadora, c.Nome, c.Dados, c.CriadoEm, c.AlteradoEm)));
    }

    [Fact]
    public async Task Restauracao_acrescenta_sem_apagar_o_historico_atual()
    {
        var origem = Criar("origem.db");
        await origem.SalvarAsync(null, "Calculadora.Ferias", "Férias", "Da cópia", "{}", Cancelamento);
        var copia = await CasoDeUso(origem).ExportarAsync(Origem, DateTime.Now, Cancelamento);
        var destino = Criar("destino.db");
        await destino.SalvarAsync(null, "Calculadora.Plr", "PLR", "Já no aparelho", "{}", Cancelamento);

        var resultado = await CasoDeUso(destino).RestaurarAsync(copia.Valor.Conteudo, Origem, Cancelamento);

        Assert.Equal(1, resultado.Valor.Incluidos);
        Assert.Equal(new[] { "Da cópia", "Já no aparelho" }, (await destino.ListarAsync(Cancelamento)).Select(c => c.Nome).Order());
    }

    [Fact]
    public async Task Historico_vazio_nao_gera_copia()
    {
        var resultado = await CasoDeUso(Criar("vazio.db")).ExportarAsync(Origem, DateTime.Now, Cancelamento);

        Assert.True(resultado.Falhou);
        Assert.Contains("Não há cálculos salvos", resultado.Erro.Mensagem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("não é json")]
    [InlineData("{\"Formato\":\"Outro\",\"Versao\":1,\"Origem\":\"Android\",\"GeradaEm\":\"2026-10-25T00:00:00\",\"Calculos\":[]}")]
    [InlineData("[1,2,3]")]
    public async Task Arquivo_desconhecido_e_recusado_sem_alterar_o_historico(string conteudo)
    {
        var historico = Criar("recusado.db");

        var resultado = await CasoDeUso(historico).RestaurarAsync(conteudo, Origem, Cancelamento);

        Assert.True(resultado.Falhou);
        Assert.Contains("não é uma cópia de segurança", resultado.Erro.Mensagem);
        Assert.Empty(await historico.ListarAsync(Cancelamento));
    }

    [Fact]
    public async Task Copia_de_outra_origem_ou_versao_mais_nova_e_recusada()
    {
        var historico = Criar("origem-errada.db");
        string Conteudo(string origem, int versao) => JsonSerializer.Serialize(new CopiaSegurancaHistoricoDto(CopiaSegurancaHistoricoUseCase.Formato, versao, origem,
            DateTime.Now, [new("Calculadora.Ferias", "Férias", "Nome", "{}", DateTime.Now, DateTime.Now)]));

        var desktop = await CasoDeUso(historico).RestaurarAsync(Conteudo("Windows", 1), Origem, Cancelamento);
        var futura = await CasoDeUso(historico).RestaurarAsync(Conteudo(Origem, 2), Origem, Cancelamento);

        Assert.Contains("aplicativo Windows", desktop.Erro.Mensagem);
        Assert.Contains("versão mais nova", futura.Erro.Mensagem);
        Assert.Empty(await historico.ListarAsync(Cancelamento));
    }

    [Fact]
    public async Task Calculo_invalido_identifica_a_posicao_e_nada_e_incluido()
    {
        var historico = Criar("invalido.db");
        var agora = new DateTime(2026, 1, 1);
        var conteudo = JsonSerializer.Serialize(new CopiaSegurancaHistoricoDto(CopiaSegurancaHistoricoUseCase.Formato, 1, Origem, agora,
        [
            new("Calculadora.Ferias", "Férias", "Válido", "{}", agora, agora),
            new("Calculadora.Ferias", "Férias", "  ", "{}", agora, agora)
        ]));

        var resultado = await CasoDeUso(historico).RestaurarAsync(conteudo, Origem, Cancelamento);

        Assert.StartsWith("Cálculo 2 da cópia: o nome", resultado.Erro.Mensagem);
        Assert.Empty(await historico.ListarAsync(Cancelamento));
    }

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }
}
