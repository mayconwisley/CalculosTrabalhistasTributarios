using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class ConsultaHistoricoPaginadoTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "historico-paginado-" + Guid.NewGuid().ToString("N"));
    private SqliteHistoricoCalculos Criar()
    {
        Directory.CreateDirectory(_pasta);
        return new(new BancoTributario(new SqliteConnectionStringBuilder { DataSource = Path.Combine(_pasta, "teste.db"), Pooling = false }.ToString()));
    }
    [Fact]
    public async Task Pagina_somente_metadados_com_ordem_estavel_e_busca_sem_acentos()
    {
        var historico = Criar();
        var ids = new List<long>();
        for (var i = 0; i < 45; i++) ids.Add(await historico.SalvarAsync(null, "Calculadora.Ferias", "Férias", $"Simulação {i:00}", new string('x', 100_000), TestContext.Current.CancellationToken));
        var vistos = new List<long>();
        for (var offset = 0; offset < 45; offset += 20)
        {
            var pagina = await historico.ConsultarAsync(new(Busca: "FERIAS", Deslocamento: offset), TestContext.Current.CancellationToken);
            Assert.Equal(45, pagina.Total); Assert.Equal(45, pagina.TotalGeral);
            Assert.Equal(new[] { "Férias" }, pagina.Calculadoras);
            Assert.InRange(pagina.Itens.Count, 1, 20);
            vistos.AddRange(pagina.Itens.Select(i => i.Id));
        }
        Assert.Equal(ids.OrderDescending(), vistos);
        var busca = await historico.ConsultarAsync(new(Busca: "simulacao 42"), TestContext.Current.CancellationToken);
        var item = Assert.Single(busca.Itens);
        Assert.Equal(ids[42], item.Id);
        Assert.Equal(100_000, (await historico.ObterAsync(item.Id, TestContext.Current.CancellationToken))?.Dados.Length);
        var literal = await historico.ConsultarAsync(new(Busca: "%' OR 1=1 --"), TestContext.Current.CancellationToken);
        Assert.Empty(literal.Itens);
    }
    [Fact]
    public async Task Filtra_datas_calculadora_e_ordena_nome_sem_alterar_historico_antigo()
    {
        var historico = Criar();
        var a = await historico.SalvarAsync(null, "Calculadora.Ferias", "Férias", "Ágata", "antigo", TestContext.Current.CancellationToken);
        var b = await historico.SalvarAsync(null, "Calculadora.Ferias", "Férias", "bruno", "outro", TestContext.Current.CancellationToken);
        await historico.SalvarAsync(null, "Calculadora.Rescisao", "Rescisão", "Zélia", "rescisao", TestContext.Current.CancellationToken);
        var pagina = await historico.ConsultarAsync(new(Calculadora: "Férias", Desde: DateTime.Today, AteExclusivo: DateTime.Today.AddDays(1), Ordem: OrdemHistorico.Nome), TestContext.Current.CancellationToken);
        Assert.Equal(new[] { a, b }, pagina.Itens.Select(i => i.Id));
        Assert.Equal(3, pagina.TotalGeral); Assert.Equal(2, pagina.Total);
        var fora = await historico.ConsultarAsync(new(Desde: DateTime.Today.AddDays(1)), TestContext.Current.CancellationToken);
        Assert.Empty(fora.Itens);
        Assert.Equal(3, (await historico.ListarAsync(TestContext.Current.CancellationToken)).Count);
    }
    [Fact]
    public async Task Consulta_valida_limites_e_observa_cancelamento()
    {
        var historico = Criar();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => historico.ConsultarAsync(new(Limite: 101), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => historico.ConsultarAsync(new(Deslocamento: -1), TestContext.Current.CancellationToken));
        using var cancelamento = new CancellationTokenSource(); cancelamento.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => historico.ConsultarAsync(new(), cancelamento.Token));
    }
    public void Dispose() { if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true); }
}
