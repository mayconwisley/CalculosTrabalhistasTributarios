using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Atualizacao;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>A versão nova baixada pelo próprio aplicativo: o instalador só é entregue se o SHA-256 conferir.</summary>
public class AtualizacaoPeloAplicativoTests
{
    private const string Conteudo = "instalador de teste";
    private static readonly Uri Pagina = new("https://github.com/mayconwisley/CalculosTrabalhistasTributarios/releases/tag/v9.9.9");
    private static readonly Uri Instalador = new("https://github.com/mayconwisley/CalculosTrabalhistasTributarios/releases/download/v9.9.9/CalculosTrabalhistasTributarios-9.9.9-setup.exe");
    private static readonly string HashCorreto = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Conteudo)));

    private static BaixadorDeAtualizacaoGitHub Baixador() => new(() => PaginasFixas.Cliente(("github.com", Conteudo)));

    [Fact]
    public async Task Instalador_com_o_hash_publicado_e_entregue()
    {
        var caminho = await Baixador().BaixarAsync(new VersaoPublicada("9.9.9", Pagina, Instalador, HashCorreto), null, default).Sucesso();

        Assert.Equal(Conteudo, File.ReadAllText(caminho));
        File.Delete(caminho);
    }

    [Fact]
    public async Task Instalador_diferente_do_publicado_e_descartado()
    {
        var erro = await Baixador().BaixarAsync(new VersaoPublicada("9.9.9", Pagina, Instalador, new string('0', 64)), null, default).Falha();

        Assert.Contains("não confere", erro.Mensagem);
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "CalculosTrabalhistasTributarios", "CalculosTrabalhistasTributarios-9.9.9-setup.exe")));
    }

    [Fact]
    public async Task Endereco_fora_dos_releases_do_projeto_nao_e_baixado()
    {
        var outroSite = new Uri("https://exemplo.com/CalculosTrabalhistasTributarios-9.9.9-setup.exe");

        var erro = await Baixador().BaixarAsync(new VersaoPublicada("9.9.9", Pagina, outroSite, HashCorreto), null, default).Falha();

        Assert.Contains("não é de um release", erro.Mensagem);
    }

    [Fact]
    public void Release_traz_o_instalador_e_o_hash_do_github()
    {
        var release = Json($$"""{"tag_name":"v9.9.9","html_url":"{{Pagina}}","body":"","assets":[{"name":"CalculosTrabalhistasTributarios-9.9.9-setup.exe","browser_download_url":"{{Instalador}}","digest":"sha256:{{HashCorreto.ToUpperInvariant()}}"}]}""");

        var versao = ConsultaVersaoGitHub.Ler(release).Sucesso();

        Assert.Equal("9.9.9", versao.Numero);
        Assert.Equal(Instalador, versao.Instalador);
        Assert.Equal(HashCorreto, versao.Sha256);
        Assert.True(versao.PodeAtualizarPeloAplicativo);
    }

    [Fact]
    public void Sem_o_hash_do_github_usa_o_das_notas_e_sem_instalador_nao_atualiza()
    {
        var comNotas = Json($$"""{"tag_name":"v9.9.9","html_url":"{{Pagina}}","body":"SHA-256 do instalador: `{{HashCorreto}}`","assets":[{"name":"CalculosTrabalhistasTributarios-9.9.9-setup.exe","browser_download_url":"{{Instalador}}"}]}""");
        var semInstalador = Json($$"""{"tag_name":"v9.9.9","html_url":"{{Pagina}}","body":"","assets":[]}""");

        Assert.Equal(HashCorreto, ConsultaVersaoGitHub.Ler(comNotas).Sucesso().Sha256);
        Assert.False(ConsultaVersaoGitHub.Ler(semInstalador).Sucesso().PodeAtualizarPeloAplicativo);
    }

    private static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement.Clone();
}
