using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class RascunhosMobileTests : IDisposable
{
    private readonly string _diretorio = Path.Combine(Path.GetTempPath(), "rascunhos-testes-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Recupera_edicoes_e_grades_em_nova_sessao_sem_associar_historico()
    {
        using var arquivos = new ArquivoRascunhosFormularios(_diretorio);
        var vm = new UsabilidadeFormularioMobileTests.FormularioTeste();
        var sessao = new SessaoRascunhoFormulario(arquivos, "Ferias", vm);
        Assert.False(await sessao.SalvarSeAlteradoAsync(sessao.Capturar(), TestContext.Current.CancellationToken));
        vm.Campos.Single(c => c.Chave == "valor").Valor = "3.500,25";
        vm.Grades[0].AdicionarLinha(new Dictionary<string, string> { ["valor"] = "123,45" });
        vm.HistoricoId = 77;
        Assert.True(await sessao.SalvarSeAlteradoAsync(sessao.Capturar(), TestContext.Current.CancellationToken));
        var novo = new UsabilidadeFormularioMobileTests.FormularioTeste { HistoricoId = 88 };
        var recuperacao = new SessaoRascunhoFormulario(arquivos, "Ferias", novo);
        var salvo = Assert.IsType<RascunhoFormularioDto>(await recuperacao.ObterAsync(TestContext.Current.CancellationToken));
        Assert.True(recuperacao.Recuperar(salvo).Sucesso);
        Assert.Equal("3.500,25", novo.Campos.Single(c => c.Chave == "valor").Valor);
        Assert.Equal("123,45", Assert.Single(novo.Grades[0].Linhas).Campo("valor").Valor);
        Assert.Null(novo.HistoricoId);
        Assert.Null(novo.Demonstrativo);
        Assert.False(await recuperacao.SalvarSeAlteradoAsync(recuperacao.Capturar(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Descarte_nao_recria_copia_ate_nova_edicao_e_separa_calculadoras()
    {
        using var arquivos = new ArquivoRascunhosFormularios(_diretorio);
        var vm = new UsabilidadeFormularioMobileTests.FormularioTeste();
        var sessao = new SessaoRascunhoFormulario(arquivos, "Ferias", vm);
        vm.Campos.Single(c => c.Chave == "valor").Valor = "10,00";
        await sessao.SalvarSeAlteradoAsync(sessao.Capturar(), TestContext.Current.CancellationToken);
        Assert.Null(await arquivos.ObterAsync("Rescisao", TestContext.Current.CancellationToken));
        await sessao.DescartarAsync(sessao.Capturar(), TestContext.Current.CancellationToken);
        Assert.False(await sessao.SalvarSeAlteradoAsync(sessao.Capturar(), TestContext.Current.CancellationToken));
        Assert.Null(await arquivos.ObterAsync("Ferias", TestContext.Current.CancellationToken));
        vm.Campos.Single(c => c.Chave == "valor").Valor = "20,00";
        Assert.True(await sessao.SalvarSeAlteradoAsync(sessao.Capturar(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"Versao\":99,\"Campos\":{},\"Grades\":{}}")]
    public void Rascunho_invalido_preserva_formulario(string json)
    {
        using var arquivos = new ArquivoRascunhosFormularios(_diretorio);
        var vm = new UsabilidadeFormularioMobileTests.FormularioTeste();
        var sessao = new SessaoRascunhoFormulario(arquivos, "Ferias", vm);
        var antes = sessao.Capturar();
        Assert.True(sessao.Recuperar(new(1, json, DateTimeOffset.UtcNow)).Falhou);
        Assert.Equal(antes, sessao.Capturar());
    }

    [Fact]
    public async Task Cancelamento_e_limite_preservam_arquivo_anterior()
    {
        using var arquivos = new ArquivoRascunhosFormularios(_diretorio);
        var anterior = new RascunhoFormularioDto(1, "anterior", DateTimeOffset.UtcNow);
        await arquivos.SalvarAsync("Ferias", anterior, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => arquivos.SalvarAsync("Ferias", anterior with { Dados = new string('a', ArquivoRascunhosFormularios.LimiteBytes) }, TestContext.Current.CancellationToken));
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => arquivos.SalvarAsync("Ferias", anterior with { Dados = "novo" }, cancelamento.Token));
        Assert.Equal(anterior, await arquivos.ObterAsync("Ferias", TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(_diretorio, "Ferias.json.tmp")));
    }

    [Fact]
    public async Task Gravacoes_concorrentes_mantem_json_valido_sem_arquivo_temporario()
    {
        using var arquivos = new ArquivoRascunhosFormularios(_diretorio);
        await Task.WhenAll(Enumerable.Range(1, 10).Select(i => arquivos.SalvarAsync("Ferias", new(1, i.ToString(), DateTimeOffset.UtcNow), TestContext.Current.CancellationToken)));
        var salvo = Assert.IsType<RascunhoFormularioDto>(await arquivos.ObterAsync("Ferias", TestContext.Current.CancellationToken));
        Assert.Contains(salvo.Dados, Enumerable.Range(1, 10).Select(i => i.ToString()));
        Assert.False(File.Exists(Path.Combine(_diretorio, "Ferias.json.tmp")));
    }

    [Fact]
    public async Task Chave_nao_permite_sair_do_diretorio()
    {
        using var arquivos = new ArquivoRascunhosFormularios(_diretorio);
        await Assert.ThrowsAsync<ArgumentException>(() => arquivos.ObterAsync("../outro", TestContext.Current.CancellationToken));
    }
    public void Dispose()
    {
        if (Directory.Exists(_diretorio)) Directory.Delete(_diretorio, true);
    }
}
