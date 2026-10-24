using CalculosTrabalhistasTributarios.Infrastructure.Arquivos;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class LimpezaCacheCompartilhamentoTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "cache-pdf-testes-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void Remove_somente_simulacoes_com_mais_de_sete_dias_e_preserva_limite()
    {
        Directory.CreateDirectory(_pasta);
        var agora = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        var antigo = Criar("simulacao-antiga.pdf", agora.AddDays(-8));
        var limite = Criar("simulacao-limite.pdf", agora.AddDays(-7));
        var recente = Criar("simulacao-recente.pdf", agora);
        var outro = Criar("outro.pdf", agora.AddDays(-8));
        Assert.Equal(0, LimpezaCacheCompartilhamento.Executar(_pasta, agora, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(antigo)); Assert.True(File.Exists(limite)); Assert.True(File.Exists(recente)); Assert.True(File.Exists(outro));
    }
    [Fact]
    public void Falha_de_um_arquivo_nao_impede_limpar_outros_ou_criar_novo_pdf()
    {
        Directory.CreateDirectory(_pasta);
        var agora = DateTime.UtcNow;
        var bloqueado = Criar("simulacao-bloqueada.pdf", agora.AddDays(-8));
        var antigo = Criar("simulacao-antiga.pdf", agora.AddDays(-8));
        // Windows preserva um arquivo aberto sem FileShare.Delete. Em outros sistemas a remoção pode ser permitida.
        using (var leitura = File.Open(bloqueado, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var falhas = LimpezaCacheCompartilhamento.Executar(_pasta, agora, TestContext.Current.CancellationToken);
            if (OperatingSystem.IsWindows()) Assert.Equal(1, falhas);
            Assert.False(File.Exists(antigo));
            File.WriteAllText(Path.Combine(_pasta, "simulacao-nova.pdf"), "novo");
            Assert.True(File.Exists(Path.Combine(_pasta, "simulacao-nova.pdf")));
        }
    }
    [Fact]
    public void Diretorio_indisponivel_e_cancelamento_tem_tratamentos_distintos()
    {
        Assert.Equal(1, LimpezaCacheCompartilhamento.Executar(_pasta, DateTime.UtcNow, TestContext.Current.CancellationToken));
        Directory.CreateDirectory(_pasta); Criar("simulacao-antiga.pdf", DateTime.UtcNow.AddDays(-8));
        using var cancelamento = new CancellationTokenSource(); cancelamento.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => LimpezaCacheCompartilhamento.Executar(_pasta, DateTime.UtcNow, cancelamento.Token));
    }
    private string Criar(string nome, DateTime modificado)
    {
        var caminho = Path.Combine(_pasta, nome); File.WriteAllText(caminho, "teste"); File.SetLastWriteTimeUtc(caminho, modificado); return caminho;
    }
    public void Dispose() { if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true); }
}
