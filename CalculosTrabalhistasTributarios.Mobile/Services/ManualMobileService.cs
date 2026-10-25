using CalculosTrabalhistasTributarios.Mobile.Presentation.Ajuda;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

/// <summary>Abre a cópia local do manual; o leitor de PDF do aparelho controla a navegação.</summary>
public sealed class ManualMobileService(ILogger<ManualMobileService> logger)
{
    public async Task AbrirAsync()
    {
        try
        {
            var pasta = Path.Combine(FileSystem.Current.CacheDirectory, "sharing-root");
            Directory.CreateDirectory(pasta);
            var caminho = Path.Combine(pasta, "ManualDoUsuario.pdf");
            if (!File.Exists(caminho))
            {
                await using var origem = await FileSystem.Current.OpenAppPackageFileAsync("ManualDoUsuario.pdf");
                await using var destino = File.Create(caminho);
                await origem.CopyToAsync(destino);
            }
            await Launcher.Default.OpenAsync(new OpenFileRequest("Manual do usuário", new ReadOnlyFile(caminho, "application/pdf")));
        }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha ao abrir o manual local no Android.");
            await Browser.Default.OpenAsync(new Uri("https://github.com/mayconwisley/CalculosTrabalhistasTributarios/blob/master/docs/MANUAL.md"),
                BrowserLaunchMode.SystemPreferred);
        }
    }

    /// <summary>Abre o print no visualizador do aparelho, que permite ampliar; a Ajuda mostra a imagem na largura da tela.</summary>
    public async Task<bool> AbrirImagemAsync(string arquivo, string descricao)
    {
        try
        {
            var pasta = Path.Combine(FileSystem.Current.CacheDirectory, "sharing-root", ManualMobile.PastaImagens);
            Directory.CreateDirectory(pasta);
            var caminho = Path.Combine(pasta, Path.GetFileName(arquivo));
            if (!File.Exists(caminho))
            {
                await using var origem = await FileSystem.Current.OpenAppPackageFileAsync(arquivo);
                await using var destino = File.Create(caminho);
                await origem.CopyToAsync(destino);
            }
            return await Launcher.Default.OpenAsync(new OpenFileRequest(descricao, new ReadOnlyFile(caminho, "image/png")));
        }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha ao abrir imagem do manual {Arquivo} no Android.", arquivo);
            return false;
        }
    }
}
