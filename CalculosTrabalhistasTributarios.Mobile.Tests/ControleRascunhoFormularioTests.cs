using System.Text.Json;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class ControleRascunhoFormularioTests
{
    [Fact]
    public async Task Nao_exporta_formulario_intacto_e_agrupa_edicoes_apos_pausa()
    {
        var formulario = new FormularioContado(); var arquivos = new Arquivos(); var relogio = new Relogio();
        using var controle = new ControleRascunhoFormulario(arquivos, "Ferias", formulario, relogio);
        await controle.PrepararAsync(false, _ => Task.FromResult(true), TestContext.Current.CancellationToken);
        var exportacoes = formulario.Exportacoes;
        for (var i = 0; i < 10; i++) Assert.False(await controle.PersistirAsync(false, TestContext.Current.CancellationToken));
        Assert.Equal(exportacoes, formulario.Exportacoes);
        formulario.Editar("10"); relogio.Avancar(600); formulario.Editar("20"); relogio.Avancar(600);
        Assert.False(await controle.PersistirAsync(false, TestContext.Current.CancellationToken));
        Assert.Equal(exportacoes, formulario.Exportacoes);
        relogio.Avancar(150);
        Assert.True(await controle.PersistirAsync(false, TestContext.Current.CancellationToken));
        Assert.Equal(1, arquivos.Gravacoes);
        Assert.Contains("20", arquivos.Salvo?.Dados);
    }
    [Fact]
    public async Task Recuperacoes_concorrentes_abrem_um_dialogo_e_nao_gravam_a_importacao()
    {
        var formulario = new FormularioContado(); var arquivos = new Arquivos { Salvo = new(1, JsonSerializer.Serialize(formulario.ExportarDados()), DateTimeOffset.UtcNow) };
        using var controle = new ControleRascunhoFormulario(arquivos, "Ferias", formulario);
        var resposta = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogos = 0;
        Task<bool> Confirmar(RascunhoFormularioDto _) { dialogos++; return resposta.Task; }
        var primeiro = controle.PrepararAsync(false, Confirmar, TestContext.Current.CancellationToken);
        var segundo = controle.PrepararAsync(false, Confirmar, TestContext.Current.CancellationToken);
        Assert.Equal(1, dialogos); resposta.SetResult(true);
        Assert.All(await Task.WhenAll(primeiro, segundo), r => Assert.True(r.Sucesso));
        Assert.Equal(1, dialogos); Assert.False(controle.TemEdicoes);
        Assert.False(await controle.PersistirAsync(true, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Edicao_durante_gravacao_nao_e_perdida_e_saida_ignora_pausa()
    {
        var formulario = new FormularioContado(); var arquivos = new Arquivos();
        using var controle = new ControleRascunhoFormulario(arquivos, "Ferias", formulario);
        await controle.PrepararAsync(true, _ => throw new InvalidOperationException(), TestContext.Current.CancellationToken);
        formulario.Editar("10");
        arquivos.Bloqueio = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var gravando = controle.PersistirAsync(true, TestContext.Current.CancellationToken);
        await arquivos.Entrou.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        formulario.Editar("30"); arquivos.Bloqueio.SetResult();
        await gravando;
        Assert.True(controle.TemEdicoes);
        Assert.True(await controle.PersistirAsync(true, TestContext.Current.CancellationToken));
        Assert.Contains("30", arquivos.Salvo?.Dados);
        Assert.False(controle.TemEdicoes);
        await controle.DescartarAsync(formulario.Inicial, TestContext.Current.CancellationToken);
        Assert.Null(arquivos.Salvo); Assert.False(controle.TemEdicoes);
    }
    [Fact]
    public async Task Edicao_durante_exclusao_do_rascunho_continua_pendente()
    {
        var formulario = new FormularioContado(); var arquivos = new Arquivos();
        using var controle = new ControleRascunhoFormulario(arquivos, "Ferias", formulario);
        await controle.PrepararAsync(true, _ => throw new InvalidOperationException(), TestContext.Current.CancellationToken);
        formulario.Editar("10"); await controle.PersistirAsync(true, TestContext.Current.CancellationToken);
        arquivos.BloqueioRemover = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var descartando = controle.DescartarAsync(formulario.Inicial, TestContext.Current.CancellationToken);
        await arquivos.Removendo.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        formulario.Editar("50"); arquivos.BloqueioRemover.SetResult();
        await descartando;
        Assert.True(controle.TemEdicoes);
        Assert.True(await controle.PersistirAsync(true, TestContext.Current.CancellationToken));
        Assert.Contains("50", arquivos.Salvo?.Dados);
    }
    [Fact]
    public void Notifica_campos_grades_e_linhas_especiais_e_remove_observadores_de_linhas_excluidas()
    {
        var formulario = new UsabilidadeFormularioMobileTests.FormularioTeste();
        var edicoes = 0; formulario.FormularioAlterado += (_, _) => edicoes++;
        formulario.Campos.Single(c => c.Chave == "valor").Valor = "100";
        Assert.True(edicoes > 0);
        edicoes = 0; var linha = formulario.Grades[0].AdicionarLinha(); Assert.True(edicoes > 0);
        edicoes = 0; Assert.NotNull(linha); linha.Campo("valor").Valor = "20"; Assert.True(edicoes > 0);
        var deposito = new LinhaDepositoFgtsMobile(); formulario.DepositosFgts.Add(deposito);
        edicoes = 0; deposito.Valor = "30"; Assert.Equal(1, edicoes);
        formulario.DepositosFgts.Clear(); edicoes = 0; deposito.Valor = "40"; Assert.Equal(0, edicoes);
    }
    private sealed class Relogio : TimeProvider
    {
        private DateTimeOffset _agora = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _agora;
        public void Avancar(int milissegundos) => _agora = _agora.AddMilliseconds(milissegundos);
    }
    private sealed class FormularioContado : IFormularioEditavelMobile
    {
        private readonly UsabilidadeFormularioMobileTests.FormularioTeste _formulario = new();
        public FormularioContado() { Inicial = _formulario.ExportarDados(); _formulario.FormularioAlterado += (_, _) => FormularioAlterado?.Invoke(this, EventArgs.Empty); }
        public event EventHandler? FormularioAlterado;
        public DadosFormularioMobile Inicial { get; }
        public int Exportacoes { get; private set; }
        public string Titulo => "Teste";
        public long? HistoricoId { get; set; }
        public DadosFormularioMobile ExportarDados() { Exportacoes++; return _formulario.ExportarDados(); }
        public Result TentarImportarDados(DadosFormularioMobile dados) => _formulario.TentarImportarDados(dados);
        public void Editar(string valor) => _formulario.Campos.Single(c => c.Chave == "valor").Valor = valor;
    }
    private sealed class Arquivos : IRascunhosFormularios
    {
        public RascunhoFormularioDto? Salvo { get; set; }
        public int Gravacoes { get; private set; }
        public TaskCompletionSource? Bloqueio { get; set; }
        public TaskCompletionSource? BloqueioRemover { get; set; }
        public TaskCompletionSource Removendo { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Entrou { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<RascunhoFormularioDto?> ObterAsync(string chave, CancellationToken cancellationToken) => Task.FromResult(Salvo);
        public async Task SalvarAsync(string chave, RascunhoFormularioDto rascunho, CancellationToken cancellationToken)
        {
            Entrou.TrySetResult();
            if (Bloqueio is { } bloqueio) await bloqueio.Task.WaitAsync(cancellationToken);
            Salvo = rascunho; Gravacoes++;
        }
        public async Task RemoverAsync(string chave, CancellationToken cancellationToken)
        {
            Removendo.TrySetResult();
            if (BloqueioRemover is { } bloqueio) await bloqueio.Task.WaitAsync(cancellationToken);
            Salvo = null;
        }
    }
}
